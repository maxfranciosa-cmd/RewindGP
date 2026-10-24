using AMS2ChEd.Business.Services.CrashDetection;
using AMS2SharedMemoryNet.Enums;
using AMS2SharedMemoryNet.Structs;
using System.Text;

namespace Ams2ChEd.Business.AMS2.Services
{
    /// <summary>
    /// AMS2's shared-memory page as a game-agnostic TelemetryFrame, for the crash detector and the
    /// telemetry recorder. Every car's speed, orientation and position come from the per-car arrays,
    /// whatever the camera shows; the damage, crash state, terrain and g-force fields exist for the
    /// viewed car only.
    /// </summary>
    public static class Ams2TelemetryFrameReader
    {
        private const double StandardGravity = 9.80665;
        private const double DegreesPerRadian = 180 / Math.PI;

        /// <summary>
        /// Null while AMS2 is halfway through writing the page (an odd sequence number): its fields
        /// may then come from two different moments. AMS2SharedMemoryNet doesn't check it itself.
        /// </summary>
        public static TelemetryFrame Read(AMS2Page page, double time)
        {
            if ((page.mSequenceNumber & 1) != 0)
                return null;

            var frame = new TelemetryFrame
            {
                Time = time,
                GameState = MapGameState(page.mGameState),
                SessionState = page.mSessionState switch
                {
                    (uint)SessionState.SESSION_FORMATION_LAP => TelemetrySessionState.FormationLap,
                    (uint)SessionState.SESSION_RACE => TelemetrySessionState.Race,
                    _ => TelemetrySessionState.Other
                },
                ViewedCarIndex = page.mViewedParticipantIndex
            };

            var participants = page.mParticipantInfo;
            if (participants == null)
                return frame;

            var count = Math.Clamp(page.mNumParticipants, 0, participants.Length);
            for (int i = 0; i < count; i++)
            {
                var participant = participants[i];
                if (participant.mIsActive == 0)
                    continue;

                var car = new TelemetryCarSample
                {
                    Index = i,
                    Name = DecodeName(participant.mName),
                    RaceState = MapRaceState(At(page.mRaceStates, i)),
                    PitState = MapPitState(At(page.mPitModes, i)),
                    Speed = At(page.mSpeeds, i),
                    // VEC_X, VEC_Y, VEC_Z: the Y angle is the heading
                    Pitch = At(page.mOrientations, i * 3) * DegreesPerRadian,
                    Yaw = At(page.mOrientations, i * 3 + 1) * DegreesPerRadian,
                    Roll = At(page.mOrientations, i * 3 + 2) * DegreesPerRadian,
                    X = At(participant.mWorldPosition, 0),
                    Y = At(participant.mWorldPosition, 1),
                    Z = At(participant.mWorldPosition, 2),
                    LapDistance = participant.mCurrentLapDistance,
                    CurrentLap = (int)participant.mCurrentLap,
                    YellowFlag = At(page.mHighestFlagColours, i) is (uint)FlagColour.FLAG_COLOUR_YELLOW or (uint)FlagColour.FLAG_COLOUR_DOUBLE_YELLOW,
                    FlagReason = At(page.mHighestFlagReasons, i) switch
                    {
                        (uint)FlagReason.FLAG_REASON_SOLO_CRASH => TelemetryFlagReason.SoloCrash,
                        (uint)FlagReason.FLAG_REASON_VEHICLE_CRASH => TelemetryFlagReason.VehicleCrash,
                        (uint)FlagReason.FLAG_REASON_VEHICLE_OBSTRUCTION => TelemetryFlagReason.Obstruction,
                        _ => TelemetryFlagReason.None
                    }
                };

                if (i == page.mViewedParticipantIndex)
                    AddViewedCarFields(page, car);

                frame.Cars.Add(car);
            }

            return frame;
        }

        public static string DecodeName(char[] raw)
        {
            if (raw == null || raw.Length == 0)
                return string.Empty;

            byte[] bytes = raw.Select(c => (byte)c).ToArray();
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0) end = bytes.Length;

            return Encoding.UTF8.GetString(bytes, 0, end);
        }

        private static void AddViewedCarFields(AMS2Page page, TelemetryCarSample car)
        {
            var acceleration = page.mLocalAcceleration ?? Array.Empty<float>();
            car.AccelerationG = Math.Sqrt(acceleration.Sum(a => (double)a * a)) / StandardGravity;

            car.CrashState = page.mCrashState switch
            {
                (uint)CrashDamage.CRASH_DAMAGE_OFFTRACK => TelemetryCrashState.Offtrack,
                (uint)CrashDamage.CRASH_DAMAGE_LARGE_PROP => TelemetryCrashState.LargeProp,
                (uint)CrashDamage.CRASH_DAMAGE_SPINNING => TelemetryCrashState.Spinning,
                (uint)CrashDamage.CRASH_DAMAGE_ROLLING => TelemetryCrashState.Rolling,
                _ => TelemetryCrashState.None
            };

            car.WallContact = (page.mTerrain ?? Array.Empty<uint>()).Any(t =>
                t is (uint)TerrainMaterials.TERRAIN_TYREWALLS or (uint)TerrainMaterials.TERRAIN_CEMENTWALLS or (uint)TerrainMaterials.TERRAIN_GUARDRAILS);
            car.WheelsDetached = (page.mTyreFlags ?? Array.Empty<uint>()).Count(f => (f & (uint)TyreFlags.TYRE_ATTACHED) == 0);
            car.AeroDamage = page.mAeroDamage;
            car.EngineDamage = page.mEngineDamage;
            car.SuspensionDamage = (page.mSuspensionDamage ?? Array.Empty<float>()).DefaultIfEmpty(0).Max();
        }

        private static TelemetryGameState MapGameState(uint gameState) => gameState switch
        {
            (uint)GameState.GAME_INGAME_PLAYING => TelemetryGameState.Playing,
            // online, the cars keep driving behind the menu
            (uint)GameState.GAME_INGAME_INMENU_TIME_TICKING => TelemetryGameState.Playing,
            (uint)GameState.GAME_INGAME_PAUSED => TelemetryGameState.Paused,
            (uint)GameState.GAME_INGAME_RESTARTING => TelemetryGameState.Restarting,
            (uint)GameState.GAME_INGAME_REPLAY or (uint)GameState.GAME_FRONT_END_REPLAY => TelemetryGameState.Replay,
            _ => TelemetryGameState.Other
        };

        private static TelemetryRaceState MapRaceState(uint raceState) => raceState switch
        {
            (uint)RaceState.RACESTATE_NOT_STARTED => TelemetryRaceState.NotStarted,
            (uint)RaceState.RACESTATE_RACING => TelemetryRaceState.Racing,
            (uint)RaceState.RACESTATE_FINISHED => TelemetryRaceState.Finished,
            (uint)RaceState.RACESTATE_DISQUALIFIED => TelemetryRaceState.Disqualified,
            (uint)RaceState.RACESTATE_RETIRED => TelemetryRaceState.Retired,
            (uint)RaceState.RACESTATE_DNF => TelemetryRaceState.Dnf,
            _ => TelemetryRaceState.Invalid
        };

        private static TelemetryPitState MapPitState(uint pitMode) => pitMode switch
        {
            (uint)PitMode.PIT_MODE_DRIVING_INTO_PITS => TelemetryPitState.DrivingIntoPits,
            (uint)PitMode.PIT_MODE_IN_PIT => TelemetryPitState.InPit,
            (uint)PitMode.PIT_MODE_DRIVING_OUT_OF_PITS => TelemetryPitState.DrivingOutOfPits,
            (uint)PitMode.PIT_MODE_IN_GARAGE => TelemetryPitState.InGarage,
            (uint)PitMode.PIT_MODE_DRIVING_OUT_OF_GARAGE => TelemetryPitState.DrivingOutOfGarage,
            _ => TelemetryPitState.None
        };

        private static float At(float[] values, int index) =>
            values != null && index >= 0 && index < values.Length ? values[index] : 0;

        private static uint At(uint[] values, int index) =>
            values != null && index >= 0 && index < values.Length ? values[index] : 0;
    }
}
