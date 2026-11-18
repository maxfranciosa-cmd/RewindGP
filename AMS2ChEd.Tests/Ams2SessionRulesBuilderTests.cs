using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2Interop;
using AMS2ChEd.Business.Models.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class Ams2SessionRulesBuilderTests
    {
        // a season with refuelling, so the fixed defaults are what the tests see
        private const int Year = 1996;

        private static Race MakeRace(string raceDate = "1996-03-10") => new Race
        {
            RaceId = 1,
            RaceName = "Australian Grand Prix",
            RaceShortName = "AUS",
            RaceDate = raceDate,
            Circuit = "Albert Park Circuit",
        };

        [TestMethod]
        public void BuildSessionRules_Default_LeavesDurationAndTimeProgressionUnset()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, defaultNumberOfLaps: 71, Year);

            Assert.IsNull(result.DurationType);
            Assert.IsNull(result.DurationValue);
            Assert.IsNull(result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_Full_UsesFullLapsAndRealTime()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Full, defaultNumberOfLaps: 71, Year);

            Assert.AreEqual(DurationType.LapBased, result.DurationType);
            Assert.AreEqual(71, result.DurationValue);
            Assert.AreEqual(TimeProgression.RealTime, result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_Half_RoundsLapsAndUsesX2()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Half, defaultNumberOfLaps: 71, Year);

            Assert.AreEqual(DurationType.LapBased, result.DurationType);
            Assert.AreEqual(36, result.DurationValue); // round(71 * 0.5) = round(35.5) = 36 (banker's rounding: 36)
            Assert.AreEqual(TimeProgression.X2, result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_OneThird_RoundsLapsAndUsesX2()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.OneThird, defaultNumberOfLaps: 71, Year);

            Assert.AreEqual(DurationType.LapBased, result.DurationType);
            Assert.AreEqual(24, result.DurationValue); // round(71 / 3.0) = round(23.67) = 24
            Assert.AreEqual(TimeProgression.X2, result.TimeProgression);
        }

        [TestMethod]
        [DataRow(Ams2RaceLength.Default, null)]
        [DataRow(Ams2RaceLength.Full, null)]
        [DataRow(Ams2RaceLength.Half, 2)]
        [DataRow(Ams2RaceLength.OneThird, 3)]
        public void GetWearMultiplier_ScalesOnlyShortenedRaces(Ams2RaceLength raceLength, int? expected)
        {
            Assert.AreEqual(expected, Ams2SessionRulesBuilder.GetWearMultiplier(raceLength));
        }

        [TestMethod]
        public void BuildSessionRules_ParsesRaceDateAsCustomDateType()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace("1996-03-10"), Ams2RaceLength.Default, 71, Year);

            Assert.AreEqual(DateType.Custom, result.DateType);
            Assert.AreEqual(new DateTime(1996, 3, 10), result.RaceDate);
        }

        [TestMethod]
        public void BuildSessionRules_UnparsableRaceDate_LeavesDateFieldsUnset()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace("not-a-date"), Ams2RaceLength.Default, 71, Year);

            Assert.IsNull(result.DateType);
            Assert.IsNull(result.RaceDate);
        }

        [TestMethod]
        public void BuildSessionRules_AlwaysAppliesTheFixedRewindGpDefaults()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, 71, Year);

            Assert.AreEqual(OnOff.Off, result.MandatoryPitStop);
            Assert.IsNull(result.PrivateQualiSession);
            Assert.AreEqual(PitTyreCount.Zero, result.PitMinTyres);
            Assert.AreEqual(PitFuelCount.Zero, result.PitMinFuel);
            Assert.AreEqual(14, result.StartHour);
        }

        [TestMethod]
        [DataRow(1975, true)]
        [DataRow(1983, true)]
        [DataRow(1984, false)]
        [DataRow(1988, false)]
        [DataRow(1993, false)]
        [DataRow(1994, true)]
        [DataRow(1996, true)]
        [DataRow(2009, true)]
        [DataRow(2010, false)]
        [DataRow(2015, false)]
        public void BuildSessionRules_RefuellingFollowsTheSeasonsEra(int seasonYear, bool allowed)
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, 71, seasonYear);

            Assert.AreEqual(allowed ? OnOff.On : OnOff.Off, result.RefuellingAllowed);
            Assert.AreEqual(!allowed, Ams2SessionRulesBuilder.IsRefuellingBanned(seasonYear));
        }

        [TestMethod]
        [DataRow(1975, 240, OnOff.Off)]
        [DataRow(1992, 240, OnOff.Off)]
        [DataRow(1993, 240, OnOff.On)]
        [DataRow(1994, 80, OnOff.On)]
        [DataRow(2025, 80, OnOff.On)]
        public void BuildSessionRules_PitLimitAndFullCourseYellowFollowTheSeasonsEra(int seasonYear, int pitKph, OnOff fcy)
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, 71, seasonYear);

            Assert.AreEqual(pitKph, result.PitSpeedLimitKph);
            Assert.AreEqual(fcy, result.FullCourseYellow);
        }

        [TestMethod]
        public void BuildSessionRules_EveryEraUsesAStandingStart()
        {
            foreach (var era in AMS2ChEd.Business.GameLogic.Concrete.RaceRulesEras.All)
                Assert.AreEqual(StartType.Grid, Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, 71, era.FirstSeason).RollingStart);
        }

        [TestMethod]
        public void BuildSessionRules_UsesTheRaceStartTime()
        {
            var race = MakeRace();
            race.StartTime = 20;

            Assert.AreEqual(20, Ams2SessionRulesBuilder.BuildSessionRules(race, Ams2RaceLength.Default, 71, Year).StartHour);
            Assert.AreEqual(20, Ams2SessionRulesBuilder.BuildQualifyingConfig(race).StartHour);
        }

        [TestMethod]
        [DataRow(-1)]
        [DataRow(24)]
        public void BuildSessionRules_OutOfRangeStartTime_FallsBackTo2pm(int startTime)
        {
            var race = MakeRace();
            race.StartTime = startTime;

            Assert.AreEqual(14, Ams2SessionRulesBuilder.BuildSessionRules(race, Ams2RaceLength.Default, 71, Year).StartHour);
        }

        [TestMethod]
        public void Race_WithoutStartTimeInJson_DefaultsTo2pm()
        {
            var race = System.Text.Json.JsonSerializer.Deserialize<Race>("{\"race_id\": 1, \"race_name\": \"Monaco Grand Prix\"}");

            Assert.AreEqual(14, race.StartTime);
        }

        [TestMethod]
        public void BuildOpponentsConfig_UsesCustomSameClassWithGivenCount()
        {
            var result = Ams2SessionRulesBuilder.BuildOpponentsConfig(19);

            Assert.AreEqual(NumOpponentsType.Custom, result.NumOpponentsType);
            Assert.AreEqual(OpponentsTypeKind.SameClass, result.OpponentsType);
            Assert.AreEqual(19, result.OpponentCount);
            Assert.IsNull(result.Skill);
            Assert.IsNull(result.AiWetWeatherSkill);
            Assert.IsNull(result.AiMistakeFrequency);
        }
    }
}
