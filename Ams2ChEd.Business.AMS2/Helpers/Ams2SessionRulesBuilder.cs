using Ams2ChEd.Business.AMS2.Settings;
using Ams2Interop;
using AMS2ChEd.Business.GameLogic.Concrete;
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
        /// <param name="seasonYear">The season's historical year: its era decides the start type and refuelling (see RaceRulesEras).</param>
        public static SessionRulesConfig BuildSessionRules(Race race, Ams2RaceLength raceLength, int defaultNumberOfLaps, int seasonYear)
        {
            var rules = RaceRulesEras.For(seasonYear);
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
                RollingStart = rules.StandingStart ? StartType.Grid : StartType.Rolling,
                PrivateQualiSession = null,
                PitMinTyres = PitTyreCount.Zero,
                PitMinFuel = PitFuelCount.Zero,
                StartHour = GetStartHour(race),
                Weather = new SessionWeatherConfig { HistoricalWeather = true },
                RefuellingAllowed = rules.RefuellingAllowed ? OnOff.On : OnOff.Off,
                PitSpeedLimitKph = rules.PitSpeedLimitKph ?? NoPitSpeedLimitKph,
                FullCourseYellow = rules.SafetyCar ? OnOff.On : OnOff.Off,
            };
        }

        /// <summary>
        /// What stands in for "no pit lane speed limit" (F1 before 1994): AMS2 always has one, and
        /// this is the highest its Rules &amp; Regulations page allows.
        /// </summary>
        public const int NoPitSpeedLimitKph = 240;

        /// <summary>True when the season's era bans refuelling - the player then starts with the fuel for the whole race.</summary>
        public static bool IsRefuellingBanned(int seasonYear) => !RaceRulesEras.For(seasonYear).RefuellingAllowed;

        /// <summary>
        /// The race's own start hour, or the 2pm default when it's missing or out of range.
        /// </summary>
        public static int GetStartHour(Race race)
        {
            var hour = race?.StartTime ?? Race.DefaultStartTime;
            return hour is >= 0 and <= 23 ? hour : Race.DefaultStartTime;
        }

        /// <summary>
        /// Qualifying config applied to every race launch (both Pre-Quali and Actual Race). Only
        /// its start hour comes from the race (so a night race also qualifies at night); the rest
        /// is fixed. Practice is deliberately
        /// left alone entirely (no PracticeQualifySessionConfig is built for it) - whatever the
        /// player already has selected for Practice stays as-is.
        ///
        /// EXPERIMENTAL - see PracticeQualifySessionConfig/SessionVmResolver's doc comments in
        /// Ams2Interop before relying on this; it isn't live-confirmed by that library yet.
        /// </summary>
        public static PracticeQualifySessionConfig BuildQualifyingConfig(Race race)
        {
            return new PracticeQualifySessionConfig
            {
                DurationValue = 30, // 30 minutes - Qualifying is always time-based
                StartHour = GetStartHour(race),
                Weather = new SessionWeatherConfig { HistoricalWeather = false, Slots = [WeatherType.Random] },
            };
        }

        /// <summary>
        /// Practice config for a rookie FP1 outing the player drives: practice switched on, 30
        /// minutes at the race's own start hour. Only applied to that launch - a normal race weekend
        /// leaves Practice alone (see BuildQualifyingConfig). EXPERIMENTAL like it.
        /// </summary>
        public static PracticeQualifySessionConfig BuildPracticeConfig(Race race)
        {
            return new PracticeQualifySessionConfig
            {
                Enabled = OnOff.On,
                DurationValue = 30,
                StartHour = GetStartHour(race),
                Weather = new SessionWeatherConfig { HistoricalWeather = false, Slots = [WeatherType.Random] },
            };
        }

        /// <summary>
        /// Tyre-wear/fuel-usage multiplier that keeps a shortened race's strategy in proportion
        /// to the full distance (half distance = x2, one third = x3). Null = leave the player's
        /// own settings alone.
        /// </summary>
        public static int? GetWearMultiplier(Ams2RaceLength raceLength)
        {
            return raceLength switch
            {
                Ams2RaceLength.Half => 2,
                Ams2RaceLength.OneThird => 3,
                _ => null,
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
