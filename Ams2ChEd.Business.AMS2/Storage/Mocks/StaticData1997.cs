using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.AMS2.DataLoaders.Mocks
{
    public class StaticData1997
    {
        public static Ams2TeamEntry FERRARI = new()
        {
            TeamId = "ferrari",
            TeamName = "Scuderia Ferrari",
            TeamPrincipal = "Jean Todt",
            Reputation = TeamReputation.TOP_TEAM,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\5_Ferrari.dds",
            BaseLiveryDriver2 = "car_liveries\\6_Ferrari.dds",
            VisorSponsors = "helmet_sponsors\\Ferrari5.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\5_Ferrari_NT.dds",
                                Driver2Livery = "car_liveries\\6_Ferrari_NT.dds",
                                VisorSponsors = "helmet_sponsors\\Ferrari5_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "schumacher", DriverNumber = 5, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "irvine", DriverNumber = 6, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0),
        };

        public static Ams2TeamEntry MCLAREN = new()
        {
            TeamId = "mclaren",
            TeamName = "McLaren Mercedes",
            TeamPrincipal = "Ron Dennis",
            Reputation = TeamReputation.MIDFIELD_HIGH,
            Ams2Car = "mclaren_mp4_12",
            BaseLiveryDriver1 = "car_liveries\\9_McLaren.dds",
            BaseLiveryDriver2 = "car_liveries\\10_Mclaren.dds",
            VisorSponsors = "helmet_sponsors\\mclaren.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\9_McLaren_NT.dds",
                                Driver2Livery = "car_liveries\\10_Mclaren.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "hakkinen", DriverNumber = 9, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "coulthard", DriverNumber = 10, Races = CommonStaticData.RACES.Count * 2 },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.05),
        };

        public static Ams2TeamEntry JORDAN = new()
        {
            TeamId = "jordan",
            TeamName = "Jordan Peugeot",
            TeamPrincipal = "Eddie Jordan",
            Reputation = TeamReputation.MIDFIELD,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\11_jordan.dds",
            BaseLiveryDriver2 = "car_liveries\\12_jordan.dds",
            VisorSponsors = "helmet_sponsors\\jordan.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\11_Jordan_NT.dds",
                                Driver2Livery = "car_liveries\\12_Jordan_NT.dds",
                                VisorSponsors = "helmet_sponsors\\jordan_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "Rschumacher", DriverNumber = 11, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "fisichella", DriverNumber = 12, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.11),
        };

        public static Ams2TeamEntry PROST = new()
        {
            TeamId = "ligier_prost",
            TeamName = "Prost Grand Prix",
            TeamPrincipal = "Alain Prost",
            Reputation = TeamReputation.MIDFIELD,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\14_Prost.dds",
            BaseLiveryDriver2 = "car_liveries\\15_Prost.dds",
            VisorSponsors = "helmet_sponsors\\prost.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\14_Prost_NT.dds",
                                Driver2Livery = "car_liveries\\15_Prost_NT.dds",
                                VisorSponsors = "helmet_sponsors\\prost_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "panis", DriverNumber = 9, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "nakano", DriverNumber = 10, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.1),
        };

        public static Ams2TeamEntry MINARDI = new()
        {
            TeamId = "minardi",
            TeamName = "Minardi Hart",
            TeamPrincipal = "Giancarlo Minardi",
            Reputation = TeamReputation.MINNOW,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\20_minardi.dds",
            BaseLiveryDriver2 = "car_liveries\\21_minardi.dds",
            Driver1Contract = new() { DriverId = "katayama", DriverNumber = 20, Races = CommonStaticData.RACES.Count },
            Driver2Contract = new() { DriverId = "trulli", DriverNumber = 21, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.2),
        };

        public static Ams2TeamEntry STEWART = new()
        {
            TeamId = "stewart",
            TeamName = "Stewart Grand Prix",
            TeamPrincipal = "Jackie Stewart",
            Reputation = TeamReputation.MIDFIELD,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\22_stewart.dds",
            BaseLiveryDriver2 = "car_liveries\\23_stewart.dds",
            Driver1Contract = new() { DriverId = "barrichello", DriverNumber = 22, Races = CommonStaticData.RACES.Count },
            Driver2Contract = new() { DriverId = "magnussen", DriverNumber = 23, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.1),
        };

        public static Ams2Season Season = new Ams2Season
        {
            Absences = new List<Absence>(),
            Ams2Class = "F-V10_Gen1",
            PointsSystem = CommonStaticData.FIRST6_POINTS_SYSTEM,
            Year = 1997,
            Races = CommonStaticData.RACES,
            Teams = new List<ITeamEntry>
                {
                    StaticData1997.FERRARI,
                    StaticData1997.MCLAREN,
                    StaticData1997.JORDAN,
                    StaticData1997.PROST,
                    StaticData1997.MINARDI,
                    StaticData1997.STEWART
                }
        };
    }
}
