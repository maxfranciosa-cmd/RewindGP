using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.AMS2.DataLoaders.Mocks
{
    public class StaticData1996
    {
        public static Ams2TeamEntry FERRARI = new()
        {
            TeamId = "ferrari",
            TeamName = "Ferrari Marlboro",
            TeamPrincipal = "Jean Todt",
            Reputation = TeamReputation.TOP_TEAM,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\1_Ferrari.dds",
            BaseLiveryDriver2 = "car_liveries\\2_Ferrari.dds",
            HelmetSponsors = "helmet_sponsors\\ferrari_helmet_sponsors.dds",
            VisorSponsors = "helmet_sponsors\\ferrari_visor_sponsors.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\1_Ferrari_NT.dds",
                                Driver2Livery = "car_liveries\\2_Ferrari_NT.dds",
                                HelmetSponsors = "helmet_sponsors\\ferrari_helmet_sponsors_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "schumacher", DriverNumber = 1, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "irvine", DriverNumber = 2, Races = CommonStaticData.RACES.Count * 2 },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0),
        };

        public static Ams2TeamEntry MCLAREN = new()
        {
            TeamId = "mclaren",
            TeamName = "McLaren Mercedes",
            TeamPrincipal = "Ron Dennis",
            Reputation = TeamReputation.MIDFIELD_HIGH,
            Ams2Car = "mclaren_mp4_12",
            BaseLiveryDriver1 = "car_liveries\\7_McLaren.dds",
            BaseLiveryDriver2 = "car_liveries\\8_Mclaren.dds",
            HelmetSponsors = "helmet_sponsors\\mclaren_helmet_sponsors.dds",
            VisorSponsors = "helmet_sponsors\\mclaren_visor_sponsors.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\7_McLaren_NT.dds",
                                Driver2Livery = "car_liveries\\8_Mclaren_NT.dds",
                                HelmetSponsors = "helmet_sponsors\\mclaren_helmet_sponsors_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "hakkinen", DriverNumber = 7, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "coulthard", DriverNumber = 8, Races = CommonStaticData.RACES.Count * 2 },
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
            HelmetSponsors = "helmet_sponsors\\jordan_helmet_sponsors.dds",
            VisorSponsors = "helmet_sponsors\\jordan_visor_sponsors.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\11_Jordan_NT.dds",
                                Driver2Livery = "car_liveries\\12_Jordan_NT.dds",
                                HelmetSponsors = "helmet_sponsors\\jordan_helmet_sponsors_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "barrichello", DriverNumber = 11, Races = CommonStaticData.RACES.Count },
            Driver2Contract = new() { DriverId = "brundle", DriverNumber = 12, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.11),
        };

        public static Ams2TeamEntry LIGIER = new()
        {
            TeamId = "ligier_prost",
            TeamName = "Team Ligier",
            Reputation = TeamReputation.MIDFIELD,
            Ams2Car = "mclaren_mp4_12",
            BaseLiveryDriver1 = "car_liveries\\9_Ligier.dds",
            BaseLiveryDriver2 = "car_liveries\\10_Ligier.dds",
            HelmetSponsors = "helmet_sponsors\\ligier_helmet_sponsors.dds",
            VisorSponsors = "helmet_sponsors\\ligier_visor_sponsors.dds",
            LiveryOverrides = new List<LiveryOverride>()
                        {
                            new()
                            {
                                RaceId = 2,
                                Driver1Livery = "car_liveries\\9_Ligier_NT.dds",
                                Driver2Livery = "car_liveries\\10_Ligier_NT.dds",
                                HelmetSponsors = "helmet_sponsors\\ligier_helmet_sponsors_NT.dds",
                                VisorSponsors = "helmet_sponsors\\ligier_visor_sponsors_NT.dds",
                            }
                        },
            Driver1Contract = new() { DriverId = "panis", DriverNumber = 9, Races = CommonStaticData.RACES.Count * 2 },
            Driver2Contract = new() { DriverId = "diniz", DriverNumber = 10, Races = CommonStaticData.RACES.Count },
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.1),
        };

        public static Ams2TeamEntry MINARDI = new()
        {
            TeamId = "minardi",
            TeamName = "Minardi Ford",
            TeamPrincipal = "Giancarlo Minardi",
            Reputation = TeamReputation.MINNOW,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\20_minardi.dds",
            BaseLiveryDriver2 = "car_liveries\\21_minardi.dds",
            VisorSponsors = "helmet_sponsors\\minardi_visor_sponsors.dds",
            Driver1Contract = new() { DriverId = "lamy", DriverNumber = 20, Races = CommonStaticData.RACES.Count },
            Driver2Contract = new() { DriverId = "fisichella", DriverNumber = 21, Races = CommonStaticData.RACES.Count},
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.2),
        };

        public static Ams2TeamEntry FORTI = new()
        {
            TeamId = "forti",
            TeamName = "Forti Corse",
            TeamPrincipal = "Guido Forti",
            Reputation = TeamReputation.SUPER_MINNOW,
            Ams2Car = "formula_v10_g1",
            BaseLiveryDriver1 = "car_liveries\\22_forti.dds",
            BaseLiveryDriver2 = "car_liveries\\23_forti.dds",
            Driver1Contract = new() { DriverId = "badoer", DriverNumber = 22, Races = CommonStaticData.RACES.Count },
            Driver2Contract = new() { DriverId = "montermini", DriverNumber = 23, Races = CommonStaticData.RACES.Count},
            Ams2CarPerformanceMalus = CommonStaticData.GetPerformanceMalus(0.25),
        };

        public static Ams2Season Season = new Ams2Season
        {
            Absences = new List<Absence>()
                {
                    new Absence()
                    {
                        RaceId = 2,
                        TeamId = "ferrari",
                        DriverOut = "irvine",
                        DriverIn = "badoer",
                        ChainedAbsence = new()
                        {
                            RaceId = 2,
                            TeamId = "forti",
                            DriverOut = "badoer",
                            DriverIn = "lavaggi"
                        }
                    }
                },
            Ams2Class = "F-V10_Gen1",
            PointsSystem = CommonStaticData.FIRST6_POINTS_SYSTEM,
            Year = 1996,
            Races = CommonStaticData.RACES,
            Teams = new List<ITeamEntry>
                {
                    StaticData1996.FERRARI,
                    StaticData1996.MCLAREN,
                    StaticData1996.JORDAN,
                    StaticData1996.LIGIER,
                    StaticData1996.MINARDI,
                    StaticData1996.FORTI
                }
        };
    }
}
