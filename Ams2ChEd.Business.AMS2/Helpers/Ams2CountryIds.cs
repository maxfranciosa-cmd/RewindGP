namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// AMS2's id for each nationality its profile can take (the "Edit info" country), by ISO 3166-1
    /// alpha-3 code. The ids are the text ids of the country names in the game's text\general.tdb.
    /// </summary>
    public static class Ams2CountryIds
    {
        // F1 timing-screen codes some season packs still carry
        private static readonly Dictionary<string, string> LegacyCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DEN"] = "DNK", ["GER"] = "DEU", ["JAP"] = "JPN", ["NED"] = "NLD", ["POR"] = "PRT", ["SUI"] = "CHE",
            ["RSA"] = "ZAF", ["UAE"] = "ARE", ["HOL"] = "NLD", ["ENG"] = "GBR", ["SCO"] = "GBR", ["WAL"] = "GBR"
        };

        private static readonly Dictionary<string, uint> Ids = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AFG"] = 0x6151BB13, // Afghanistan
            ["AGO"] = 0xA61631F9, // Angola
            ["ALB"] = 0xD31E2AA6, // Albania
            ["AND"] = 0xC7926CC9, // Andorra
            ["ARE"] = 0xED101928, // United Arab Emirates
            ["ARG"] = 0x3E7DD65A, // Argentina
            ["ARM"] = 0x0C7D1123, // Armenia
            ["ATG"] = 0x2048B9C5, // Antigua & Deps
            ["AUS"] = 0xD6C318BB, // Australia
            ["AUT"] = 0x41224491, // Austria
            ["AZE"] = 0xDE3CBAAB, // Azerbaijan
            ["BDI"] = 0xEC03F4D8, // Burundi
            ["BEL"] = 0x791D3375, // Belgium
            ["BEN"] = 0x2BE23A33, // Benin
            ["BFA"] = 0xCD0E641F, // Burkina
            ["BGD"] = 0xA1EB1C86, // Bangladesh
            ["BGR"] = 0x497309FA, // Bulgaria
            ["BHR"] = 0x09A1E4FD, // Bahrain
            ["BHS"] = 0x1D57838B, // Bahamas
            ["BIH"] = 0x3025699C, // Bosnia Herzegovina
            ["BLR"] = 0x7DFDD7FE, // Belarus
            ["BLZ"] = 0x0DC2EDE1, // Belize
            ["BOL"] = 0x8AEB2D97, // Bolivia
            ["BRA"] = 0x379F2C38, // Brazil
            ["BRB"] = 0xBFA12771, // Barbados
            ["BRN"] = 0x387EB495, // Brunei
            ["BTN"] = 0x5BF9A205, // Bhutan
            ["BWA"] = 0xDA0D5224, // Botswana
            ["CAF"] = 0x79FD5F76, // Central African Rep
            ["CAN"] = 0xEF7065D2, // Canada
            ["CHE"] = 0xBA47D4C9, // Switzerland
            ["CHL"] = 0xAA5D6821, // Chile
            ["CHN"] = 0xB2E877B6, // China
            ["CIV"] = 0x025D1BBE, // Ivory Coast
            ["CMR"] = 0xBE5086E9, // Cameroon
            ["COD"] = 0x0A9206D1, // Congo {Democratic Rep}
            ["COG"] = 0xE674A212, // Congo
            ["COL"] = 0xFF1D1D59, // Colombia
            ["COM"] = 0x2C31D17D, // Comoros
            ["CPV"] = 0x69D2F8BB, // Cape Verde
            ["CRI"] = 0x31950110, // Costa Rica
            ["CUB"] = 0x11DDC239, // Cuba
            ["CYP"] = 0xADECB8FA, // Cyprus
            ["CZE"] = 0x77F0CA9B, // Czech Republic
            ["DEU"] = 0xEA82CAA2, // Germany
            ["DJI"] = 0x1129CF67, // Djibouti
            ["DMA"] = 0x06439CEA, // Dominica
            ["DNK"] = 0x0ED5E95C, // Denmark
            ["DOM"] = 0x08503E0F, // Dominican Republic
            ["DZA"] = 0x1940FE6F, // Algeria
            ["ECU"] = 0xC0E3CB34, // Ecuador
            ["EGY"] = 0xBC7713A0, // Egypt
            ["ERI"] = 0x8BD65383, // Eritrea
            ["ESP"] = 0x0BA5BE90, // Spain
            ["EST"] = 0x2922E1AE, // Estonia
            ["ETH"] = 0x5FD45511, // Ethiopia
            ["FIN"] = 0x3A6096A3, // Finland
            ["FJI"] = 0x07E598D1, // Fiji
            ["FRA"] = 0xFFE1DE2B, // France
            ["FSM"] = 0x4693E425, // Micronesia
            ["GAB"] = 0x94295FA7, // Gabon
            ["GBR"] = 0x759924D0, // United Kingdom
            ["GEO"] = 0xDD1E98E2, // Georgia
            ["GHA"] = 0xA6BD49B7, // Ghana
            ["GIN"] = 0x633C1B68, // Guinea
            ["GMB"] = 0xC063BC2B, // Gambia
            ["GNB"] = 0x95029CDD, // Guinea-Bissau
            ["GNQ"] = 0x0C8B9937, // Equatorial Guinea
            ["GRC"] = 0xC0122E6C, // Greece
            ["GRD"] = 0x04880242, // Grenada
            ["GTM"] = 0x56C7EE5A, // Guatemala
            ["GUY"] = 0xC8B5E219, // Guyana
            ["HKG"] = 0xA24C36BD, // Hong Kong
            ["HND"] = 0x7A62EB5C, // Honduras
            ["HRV"] = 0xC76FBC6E, // Croatia
            ["HTI"] = 0xBD66672E, // Haiti
            ["HUN"] = 0xAA1308F1, // Hungary
            ["IDN"] = 0x6BC44AF4, // Indonesia
            ["IND"] = 0x2DE7A615, // India
            ["IRL"] = 0x08EF031D, // Republic of Ireland
            ["IRN"] = 0xA3F637DA, // Iran
            ["IRQ"] = 0x7FFE45FB, // Iraq
            ["ISL"] = 0x2335D7EA, // Iceland
            ["ISR"] = 0x0B0594FE, // Israel
            ["ITA"] = 0x2CEC4167, // Italy
            ["JAM"] = 0xA56ECC71, // Jamaica
            ["JOR"] = 0x1F2B4141, // Jordan
            ["JPN"] = 0x8BE18EDC, // Japan
            ["KAZ"] = 0x43707C80, // Kazakhstan
            ["KEN"] = 0x140F2052, // Kenya
            ["KGZ"] = 0x12279C5C, // Kyrgyzstan
            ["KHM"] = 0x032A3745, // Cambodia
            ["KIR"] = 0x4BBEAAD8, // Kiribati
            ["KNA"] = 0x99C492E6, // St Kitts & Nevis
            ["KOR"] = 0xC6D57314, // South Korea
            ["KWT"] = 0x9C712FE0, // Kuwait
            ["LAO"] = 0x8B117572, // Laos
            ["LBN"] = 0xE19012DC, // Lebanon
            ["LBR"] = 0xDD3291EA, // Liberia
            ["LBY"] = 0x79CD6993, // Libya
            ["LCA"] = 0x2FBB955A, // St Lucia
            ["LIE"] = 0x95C99860, // Liechtenstein
            ["LKA"] = 0xC3A51514, // Sri Lanka
            ["LSO"] = 0x3A8FE95A, // Lesotho
            ["LTU"] = 0xC2476985, // Lithuania
            ["LUX"] = 0x53BDE253, // Luxembourg
            ["LVA"] = 0x0F65A9FA, // Latvia
            ["MAR"] = 0xC3D2C401, // Morocco
            ["MCO"] = 0xA974FAAC, // Monaco
            ["MDA"] = 0x71AD087D, // Moldova
            ["MDG"] = 0x2359B187, // Madagascar
            ["MDV"] = 0xFD9E0CD9, // Maldives
            ["MEX"] = 0x151A3055, // Mexico
            ["MHL"] = 0x836C6B4D, // Marshall Islands
            ["MKD"] = 0x286313C3, // Macedonia
            ["MLI"] = 0xB45C45A8, // Mali
            ["MLT"] = 0x9E3D7C68, // Malta
            ["MMR"] = 0x9FF7F893, // Myanmar, {Burma}
            ["MNE"] = 0xAF1A4C34, // Montenegro
            ["MNG"] = 0x3BA70DF6, // Mongolia
            ["MOZ"] = 0x8526A723, // Mozambique
            ["MRT"] = 0x85C2A5BB, // Mauritania
            ["MUS"] = 0xEE8D1413, // Mauritius
            ["MWI"] = 0xE1658252, // Malawi
            ["MYS"] = 0x3A3CCEB2, // Malaysia
            ["NAM"] = 0x0306FC15, // Namibia
            ["NER"] = 0x7D78B69B, // Niger
            ["NGA"] = 0x04344809, // Nigeria
            ["NIC"] = 0x1DFEBFC0, // Nicaragua
            ["NLD"] = 0x6AAA3EBE, // Netherlands
            ["NOR"] = 0xBA92E983, // Norway
            ["NPL"] = 0x7815590E, // Nepal
            ["NRU"] = 0x564E17A4, // Nauru
            ["NZL"] = 0x809765A3, // New Zealand
            ["OMN"] = 0x8DDE4C7D, // Oman
            ["PAK"] = 0xE259C0FB, // Pakistan
            ["PAN"] = 0x1319F2DB, // Panama
            ["PER"] = 0xBD1D4460, // Peru
            ["PHL"] = 0x8BD9F47C, // Philippines
            ["PLW"] = 0xCCB2F384, // Palau
            ["PNG"] = 0x16F10F36, // Papua New Guinea
            ["POL"] = 0x5E405728, // Poland
            ["PRK"] = 0x27BA860C, // North Korea
            ["PRT"] = 0xBCF9A504, // Portugal
            ["PRY"] = 0x4430713A, // Paraguay
            ["QAT"] = 0xCA7A95A5, // Qatar
            ["ROU"] = 0xAD52D5C0, // Romania
            ["RUS"] = 0x10427279, // Russian Federation
            ["RWA"] = 0x726FA55F, // Rwanda
            ["SAU"] = 0x31AA2A07, // Saudi Arabia
            ["SDN"] = 0xDD946AC3, // Sudan
            ["SEN"] = 0x0864CD01, // Senegal
            ["SGP"] = 0xFDA98235, // Singapore
            ["SLB"] = 0x39FCCF66, // Solomon Islands
            ["SLE"] = 0x71D13408, // Sierra Leone
            ["SLV"] = 0x533992F4, // El Salvador
            ["SMR"] = 0x5D9223F8, // San Marino
            ["SOM"] = 0xE4C498D4, // Somalia
            ["SRB"] = 0xCFC5E56B, // Serbia
            ["SSD"] = 0x1CA4280B, // South Sudan
            ["STP"] = 0x4E84AA26, // Sao Tome & Principe
            ["SUR"] = 0xAABA8FBA, // Suriname
            ["SVK"] = 0x8F077BDC, // Slovakia
            ["SVN"] = 0xE2BCA676, // Slovenia
            ["SWE"] = 0xD49302F4, // Sweden
            ["SWZ"] = 0xB76D42EA, // Swaziland
            ["SYC"] = 0x1E155BF2, // Seychelles
            ["SYR"] = 0x0D0AC5F8, // Syria
            ["TCD"] = 0x174E8081, // Chad
            ["TGO"] = 0xCE63777B, // Togo
            ["THA"] = 0x2B08006A, // Thailand
            ["TJK"] = 0xEC0CA5BF, // Tajikistan
            ["TKM"] = 0xAE1CDBC0, // Turkmenistan
            ["TLS"] = 0x4899EF05, // East Timor
            ["TON"] = 0xBB458E6B, // Tonga
            ["TTO"] = 0x4545FB59, // Trinidad & Tobago
            ["TUN"] = 0xC21F25F0, // Tunisia
            ["TUR"] = 0x0CEAE7A7, // Turkey
            ["TUV"] = 0xD5E95EE0, // Tuvalu
            ["TWN"] = 0x962E8AB9, // Taiwan
            ["TZA"] = 0xF1E2578E, // Tanzania
            ["UGA"] = 0x7954A00A, // Uganda
            ["UKR"] = 0x8520D1E3, // Ukraine
            ["URY"] = 0x3BD969CB, // Uruguay
            ["USA"] = 0x7F78B849, // United States
            ["UZB"] = 0x2C101EFA, // Uzbekistan
            ["VAT"] = 0x858FE44D, // Vatican City
            ["VCT"] = 0xB58B3412, // Saint Vincent & the Grenadines
            ["VEN"] = 0xDF6D6987, // Venezuela
            ["VNM"] = 0x4ED7D76B, // Vietnam
            ["VUT"] = 0xE9E0CD54, // Vanuatu
            ["WSM"] = 0x4F54C3D5, // Samoa
            ["XKX"] = 0x82F44A49, // Kosovo
            ["YEM"] = 0x0FC11580, // Yemen
            ["ZAF"] = 0x2CD9A712, // South Africa
            ["ZMB"] = 0xFE764FAB, // Zambia
            ["ZWE"] = 0x7048526C, // Zimbabwe
        };

        /// <summary>The AMS2 country id for a nationality code, or null when AMS2 has no such country.</summary>
        public static uint? For(string nationality)
        {
            if (string.IsNullOrWhiteSpace(nationality))
                return null;
            string code = LegacyCodes.TryGetValue(nationality.Trim(), out var iso) ? iso : nationality.Trim();
            return Ids.TryGetValue(code, out var id) ? id : null;
        }
    }
}