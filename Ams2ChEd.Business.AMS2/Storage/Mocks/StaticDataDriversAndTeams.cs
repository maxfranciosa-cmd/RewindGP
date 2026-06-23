using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.DataLoaders.Mocks;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace Ams2ChEd.Business.AMS2.Storage.Mocks
{
    public class StaticDataDriversAndTeams
    {
        public static Dictionary<string, Team> TEAMS = new() {
            {  "ferrari" , new() { TeamId = "ferrari", TeamName = "Ferrari" } },
            {  "mclaren" , new() { TeamId = "mclaren", TeamName = "McLaren" } },
            {  "jordan" , new() { TeamId = "jordan", TeamName = "Jordan" } },
            {  "ligier_prost" , new() { TeamId = "ligier_prost", TeamName = "Ligier / Prost" } },
            {  "minardi" , new() { TeamId = "minardi", TeamName = "Minardi" } },
            {  "forti" , new() { TeamId = "forti", TeamName = "Forti Corse" } },
            {  "stewart" , new() { TeamId = "stewart", TeamName = "Stewart Grand Prix" } },
        };

        public static Dictionary<string, Ams2DriverData> DRIVERS_96 = new()
        {
            { "schumacher",
                new Ams2DriverData {
                    DriverId = "schumacher",
                    Name = "Michael Schumacher",
                    Nationality = "DEU",
                    YearOfBirth = 1969,
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                    RatingValues = CommonStaticData.GOOD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "numacher.dds",
                    BaseVisorFile = "numacher_visor.dds"
                }
            },
            { "irvine",
                new Ams2DriverData() {
                    DriverId = "irvine",
                    Name = "Eddie Irvine",
                    Nationality = "GBR",
                    YearOfBirth = 1965,
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "irviton.dds",
                    BaseVisorFile = "irviton_visor.dds"
                }
            },
            { "hakkinen",
                new Ams2DriverData() {
                    DriverId = "hakkinen",
                    Name = "Mika Hakkinen",
                    Nationality = "FIN",
                    YearOfBirth = 1968,
                    Reputation = DriverReputation.PRIME_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "kakkinen.dds",
                    BaseVisorFile = "kakkinen_visor.dds"
                }
            },
            { "coulthard",
                new Ams2DriverData() {
                    DriverId = "coulthard",
                    Name = "David Coulthard",
                    Nationality = "GBR",
                    YearOfBirth = 1971,
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "mallard.dds",
                    BaseVisorFile = "mallard_visor.dds"
                }
            },
            { "barrichello",
                new Ams2DriverData() {
                    DriverId = "barrichello",
                    Name = "Rubens Barrichello",
                    Nationality = "BRA",
                    YearOfBirth = 1972,
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "carrello.dds",
                    BaseVisorFile = "carrello_visor.dds"
                }
            },
            { "brundle",
                new Ams2DriverData() {
                    DriverId = "brundle",
                    Name = "Martin Brundle",
                    YearOfBirth = 1959,
                    Nationality = "GBR",
                    Reputation = DriverReputation.AGEING_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "axelson.dds",
                    BaseVisorFile = "axelson_visor.dds"
                }
            },
            { "lamy",
                new Ams2DriverData() {
                    DriverId = "lamy",
                    Name = "Pedro Lamy",
                    YearOfBirth = 1972,
                    Nationality = "POR",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "namy.dds",
                    BaseVisorFile = "namy_visor.dds"
                }
            },
            { "fisichella",
                new Ams2DriverData() {
                    DriverId = "fisichella",
                    Name = "Giancarlo Fisichella",
                    YearOfBirth = 1973,
                    Nationality = "ITA",
                    Reputation = DriverReputation.YOUNG_TALENT,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "fisiketio.dds",
                    BaseVisorFile = "fisiketio_visor.dds"
                }
            },
            { "badoer",
                new Ams2DriverData() {
                    DriverId = "badoer",
                    Name = "Luca Badoer",
                    YearOfBirth = 1971,
                    Nationality = "ITA",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "somaru.dds",
                    BaseVisorFile = "somaru_visor.dds"
                }
            },
            { "montermini",
                new Ams2DriverData() {
                    DriverId = "montermini",
                    Name = "Andrea Montermini",
                    YearOfBirth = 1964,
                    Nationality = "ITA",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "bontempi.dds",
                    BaseVisorFile = "bontempi_visor.dds"
                }
            },
            { "panis",
                new Ams2DriverData() {
                    DriverId = "panis",
                    Name = "Olivier Panis",
                    YearOfBirth = 1966,
                    Nationality = "FRA",
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "panin.dds",
                    BaseVisorFile = "panin_visor.dds"
                }
            },
            { "diniz",
                new Ams2DriverData() {
                    DriverId = "diniz",
                    Name = "Pedro Diniz",
                    YearOfBirth = 1970,
                    Nationality = "BRA",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "esquecido.dds",
                    BaseVisorFile = "esquecido_visor.dds"
                }
            },
            { "lavaggi",
                new Ams2DriverData() {
                    DriverId = "lavaggi",
                    Name = "Giovanni Lavaggi",
                    YearOfBirth = 1958,
                    Nationality = "ITA",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "esquecido.dds",
                    BaseVisorFile = "esquecido_visor.dds"
                }
            },
            { "katayama",
                new Ams2DriverData() {
                    DriverId = "katayama",
                    Name = "Ukyo Katayama",
                    YearOfBirth = 1963,
                    Nationality = "JPN",
                    Reputation = DriverReputation.AGEING_MIDFIELD,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "kafeyama.dds",
                    BaseVisorFile = "kafeyama_visor.dds"
                }
            },
        };

        public static Dictionary<string, Ams2DriverData> DRIVERS_97 = new()
        {
            { "schumacher",
                new Ams2DriverData() {
                    DriverId = "schumacher",
                    Name = "Michael Schumacher",
                    Nationality = "DEU",
                    YearOfBirth = 1969,
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                    RatingValues = CommonStaticData.GOOD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "numacher.dds",
                    BaseVisorFile = "numacher_visor.dds"
                }
            },
            { "irvine",
                new Ams2DriverData() {
                    DriverId = "irvine",
                    Name = "Eddie Irvine",
                    Nationality = "GBR",
                    YearOfBirth = 1965,
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "irviton.dds",
                    BaseVisorFile = "irviton_visor.dds"
                }
            },
            { "hakkinen",
                new Ams2DriverData() {
                    DriverId = "hakkinen",
                    Name = "Mika Hakkinen",
                    Nationality = "FIN",
                    YearOfBirth = 1968,
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "kakkinen.dds",
                    BaseVisorFile = "kakkinen_visor.dds"
                }
            },
            { "coulthard",
                new Ams2DriverData() {
                    DriverId = "coulthard",
                    Name = "David Coulthard",
                    Nationality = "GBR",
                    YearOfBirth = 1971,
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "mallard.dds",
                    BaseVisorFile = "mallard_visor.dds"
                }
            },
            { "barrichello",
                new Ams2DriverData() {
                    DriverId = "barrichello",
                    Name = "Rubens Barrichello",
                    Nationality = "BRA",
                    YearOfBirth = 1972,
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "carrello.dds",
                    BaseVisorFile = "carrello_visor.dds"
                }
            },
            { "fisichella",
                new Ams2DriverData() {
                    DriverId = "fisichella",
                    Name = "Giancarlo Fisichella",
                    YearOfBirth = 1973,
                    Nationality = "ITA",
                    Reputation = DriverReputation.YOUNG_TALENT,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "fisiketio.dds",
                    BaseVisorFile = "fisiketio_visor.dds"
                }
            },
            { "panis",
                new Ams2DriverData() {
                    DriverId = "panis",
                    Name = "Olivier Panis",
                    YearOfBirth = 1966,
                    Nationality = "FRA",
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "panin.dds",
                    BaseVisorFile = "panin_visor.dds"
                }
            },
            { "diniz",
                new Ams2DriverData() {
                    DriverId = "diniz",
                    Name = "Pedro Diniz",
                    YearOfBirth = 1970,
                    Nationality = "BRA",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "esquecido.dds",
                    BaseVisorFile = "esquecido_visor.dds"
                }
            },
            { "Rschumacher",
                new Ams2DriverData() {
                    DriverId = "Rschumacher",
                    Name = "Ralf Schumacher",
                    YearOfBirth = 1975 ,
                    Nationality = "DEU",
                    Reputation = DriverReputation.YOUNG_TALENT,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "borsett.dds",
                    BaseVisorFile = "borsett_visor.dds"
                }
            },
            { "trulli",
                new Ams2DriverData() {
                    DriverId = "trulli",
                    Name = "Jarno Trulli",
                    YearOfBirth = 1974 ,
                    Nationality = "ITA",
                    Reputation = DriverReputation.YOUNG_TALENT,
                    RatingValues = CommonStaticData.MEDIUM_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "sgrulli.dds",
                    BaseVisorFile = "sgrulli_visor.dds"
                }
            },
            { "nakano",
                new Ams2DriverData() {
                    DriverId = "nakano",
                    Name = "Shinji Nakano",
                    YearOfBirth = 1971 ,
                    Nationality = "JPN",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "kraften.dds",
                    BaseVisorFile = "kraften_visor.dds"
                }
            },
            { "katayama",
                new Ams2DriverData() {
                    DriverId = "katayama",
                    Name = "Ukyo Katayama",
                    YearOfBirth = 1963,
                    Nationality = "JPN",
                    Reputation = DriverReputation.AGEING_MIDFIELD,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "kafeyama.dds",
                    BaseVisorFile = "kafeyama_visor.dds"
                }
            },
            { "marques",
                new Ams2DriverData() {
                    DriverId = "marques",
                    Name = "Tarso Marques",
                    YearOfBirth = 1976,
                    Nationality = "BRA",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "maddeques.dds",
                    BaseVisorFile = "maddeques_visor.dds"
                }
            },
            { "magnussen",
                new Ams2DriverData() {
                    DriverId = "magnussen",
                    Name = "Jan Magnussen",
                    YearOfBirth = 1973,
                    Nationality = "DAN",
                    Reputation = DriverReputation.YOUNG_TALENT,
                    RatingValues = CommonStaticData.BAD_DRIVER_RATING_VALUES,
                    BaseHelmetFile = "avvocappen.dds",
                    BaseVisorFile = "avvocappen_visor.dds"
                }
            },
        };
    }
}