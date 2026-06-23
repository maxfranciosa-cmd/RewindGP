using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    [JsonConverter(typeof(DriverReputationConverter))]
    public enum DriverReputation
    {
        PAY_DRIVER_WILD_CARD,
        PAY_DRIVER_SEASON,
        AGEING_MIDFIELD,
        YOUNG_TALENT,
        PRIME_MIDFIELD,
        AGEING_STRONG_MIDFIELD,
        JUST_ONE_LAST_DANCE,
        PRIME_STRONG_MIDFIELD,
        AGEING_CHAMPIONSHIP_LEVEL_WASHED,
        PRIME_CHAMPIONSHIP_LEVEL_WASHED,
        PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN,
        YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
        AGEING_CHAMPIONSHIP_LEVEL,
        PRIME_CHAMPIONSHIP_LEVEL,
        YOUNG_CHAMPIONSHIP_LEVEL
    }
    [JsonConverter(typeof(TeamReputationConverter))]
    public enum TeamReputation
    {
        SUPER_MINNOW,
        MINNOW,
        MIDFIELD,
        MIDFIELD_HIGH,
        TOP_TEAM
    }
}
