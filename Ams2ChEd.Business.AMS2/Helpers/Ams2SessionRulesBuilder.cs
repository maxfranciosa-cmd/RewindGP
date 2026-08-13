using Ams2ChEd.Business.AMS2.Settings;
using Ams2Interop;
using AMS2ChEd.Business.Models.Concrete;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Pure mapping from Rewind GP's race-length/AI-opponent concepts onto the
    /// Ams2Interop.SessionRulesConfig/OpponentsConfig shapes Ams2RaceConfigurator.ApplyRaceConfigAsync
    /// expects. Kept separate from Ams2RaceLaunchAssistant's orchestration so the mapping itself is
    /// unit-testable without a live game process.
    /// </summary>
    public static class Ams2SessionRulesBuilder
    {
        public static SessionRulesConfig BuildSessionRules(Race race, Ams2RaceLength raceLength, int defaultNumberOfLaps)
        {
            DurationType? durationType = null;
            int? durationValue = null;
            TimeProgression? timeProgression = null;

            switch (raceLength)
            {
                case Ams2RaceLength.Full:
                    durationType = DurationType.LapBased;
                    durationValue = defaultNumberOfLaps;
                    timeProgression = TimeProgression.RealTime;
                    break;
                case Ams2RaceLength.Half:
                    durationType = DurationType.LapBased;
                    durationValue = (int)Math.Round(defaultNumberOfLaps * 0.5);
                    timeProgression = TimeProgression.X2;
                    break;
                case Ams2RaceLength.OneThird:
                    durationType = DurationType.LapBased;
                    durationValue = (int)Math.Round(defaultNumberOfLaps / 3.0);
                    timeProgression = TimeProgression.X2;
                    break;
                case Ams2RaceLength.Default:
                default:
                    // Leave DurationType/DurationValue/TimeProgression null - don't force.
                    break;
            }

            DateTime? raceDate = DateTime.TryParse(race?.RaceDate, out var parsed) ? parsed : null;

            return new SessionRulesConfig
            {
                RaceDate = raceDate,
                DateType = raceDate.HasValue ? DateType.Custom : null,
                DurationType = durationType,
                DurationValue = durationValue,
                TimeProgression = timeProgression,
                MandatoryPitStop = OnOff.Off,
                RollingStart = StartType.Grid,
                PrivateQualiSession = null,
                PitMinTyres = PitTyreCount.Zero,
                PitMinFuel = PitFuelCount.Zero,
                StartHour = 14,
                Weather = new SessionWeatherConfig { HistoricalWeather = true },
                RefuellingAllowed = OnOff.On
            };
        }

        /// <summary>
        /// Fixed Qualifying config applied to every race launch (both Pre-Quali and Actual Race) -
        /// not derived from Race/season data, unlike BuildSessionRules. Practice is deliberately
        /// left alone entirely (no PracticeQualifySessionConfig is built for it) - whatever the
        /// player already has selected for Practice stays as-is.
        ///
        /// EXPERIMENTAL - see PracticeQualifySessionConfig/SessionVmResolver's doc comments in
        /// Ams2Interop before relying on this; it isn't live-confirmed by that library yet.
        /// </summary>
        public static PracticeQualifySessionConfig BuildQualifyingConfig()
        {
            return new PracticeQualifySessionConfig
            {
                DurationValue = 60, // 1 hour, in minutes - Qualifying is always time-based
                StartHour = 14,     // 2pm
                Weather = new SessionWeatherConfig { HistoricalWeather = false, Slots = [WeatherType.Random] },
            };
        }

        public static OpponentsConfig BuildOpponentsConfig(int opponentCount)
        {
            return new OpponentsConfig
            {
                NumOpponentsType = NumOpponentsType.Custom,
                OpponentsType = OpponentsTypeKind.SameClass,
                OpponentCount = opponentCount,
                Skill = null,
                AiWetWeatherSkill = null,
                AiMistakeFrequency = null,
            };
        }
    }
}
