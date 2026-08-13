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
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, defaultNumberOfLaps: 71);

            Assert.IsNull(result.DurationType);
            Assert.IsNull(result.DurationValue);
            Assert.IsNull(result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_Full_UsesFullLapsAndRealTime()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Full, defaultNumberOfLaps: 71);

            Assert.AreEqual(DurationType.LapBased, result.DurationType);
            Assert.AreEqual(71, result.DurationValue);
            Assert.AreEqual(TimeProgression.RealTime, result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_Half_RoundsLapsAndUsesX2()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Half, defaultNumberOfLaps: 71);

            Assert.AreEqual(DurationType.LapBased, result.DurationType);
            Assert.AreEqual(36, result.DurationValue); // round(71 * 0.5) = round(35.5) = 36 (banker's rounding: 36)
            Assert.AreEqual(TimeProgression.X2, result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_OneThird_RoundsLapsAndUsesX2()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.OneThird, defaultNumberOfLaps: 71);

            Assert.AreEqual(DurationType.LapBased, result.DurationType);
            Assert.AreEqual(24, result.DurationValue); // round(71 / 3.0) = round(23.67) = 24
            Assert.AreEqual(TimeProgression.X2, result.TimeProgression);
        }

        [TestMethod]
        public void BuildSessionRules_ParsesRaceDateAsCustomDateType()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace("1996-03-10"), Ams2RaceLength.Default, 71);

            Assert.AreEqual(DateType.Custom, result.DateType);
            Assert.AreEqual(new DateTime(1996, 3, 10), result.RaceDate);
        }

        [TestMethod]
        public void BuildSessionRules_UnparsableRaceDate_LeavesDateFieldsUnset()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace("not-a-date"), Ams2RaceLength.Default, 71);

            Assert.IsNull(result.DateType);
            Assert.IsNull(result.RaceDate);
        }

        [TestMethod]
        public void BuildSessionRules_AlwaysAppliesTheFixedRewindGpDefaults()
        {
            var result = Ams2SessionRulesBuilder.BuildSessionRules(MakeRace(), Ams2RaceLength.Default, 71);

            Assert.AreEqual(OnOff.Off, result.MandatoryPitStop);
            Assert.AreEqual(StartType.Grid, result.RollingStart);
            Assert.IsNull(result.PrivateQualiSession);
            Assert.AreEqual(PitTyreCount.Zero, result.PitMinTyres);
            Assert.AreEqual(PitFuelCount.Zero, result.PitMinFuel);
            Assert.AreEqual(14, result.StartHour);
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
