using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Resources;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// What a driver market outlook (IMarketOutlookEstimator) says, in words: the season it is worked out
    /// against, the names of its drivers and teams, and the wording of its seats. Shared by
    /// MarketOutlookWindow and AgentTalkWindow, so the agent passes on the same outlook the report shows.
    /// </summary>
    public class MarketOutlookText
    {
        // rivals named in a seat's reason before the rest are counted
        private const int NamedRivals = 3;

        private readonly ISaveGame _saveGame;
        private readonly Dictionary<string, IDriverData> _drivers;
        private readonly Dictionary<string, ITeamEntry> _teams;

        public ISeason NextSeason { get; }
        public IReadOnlyDictionary<string, IDriverData> NextSeasonDrivers { get; }

        private MarketOutlookText(ISaveGame saveGame, ISeason nextSeason, IReadOnlyDictionary<string, IDriverData> nextSeasonDrivers)
        {
            _saveGame = saveGame;
            NextSeason = nextSeason;
            NextSeasonDrivers = nextSeasonDrivers;

            _drivers = nextSeasonDrivers.Values
                .Concat(saveGame.Drivers ?? Enumerable.Empty<IDriverData>())
                .Concat(saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .GroupBy(d => d.DriverId)
                .ToDictionary(g => g.Key, g => g.Last());
            _teams = saveGame.CurrentSeason.Teams
                .Concat(nextSeason.Teams)
                .GroupBy(t => t.TeamId)
                .ToDictionary(g => g.Key, g => g.Last());
        }

        // next season's pack, or this season's again when it isn't installed (as the off-season does)
        public static MarketOutlookText Create(ISaveGame saveGame, IGameDataFactory dataFactory)
        {
            var nextSeasonYear = saveGame.CurrentSeason.Year + 1;
            if (dataFactory.SeasonLoader.GetAvailableSeasons().Contains(nextSeasonYear.ToString()))
            {
                return new MarketOutlookText(saveGame, dataFactory.SeasonLoader.LoadBaseSeason(nextSeasonYear), dataFactory.DriversLoader.LoadDriversBase(nextSeasonYear));
            }

            var sameSeasonAgain = dataFactory.SeasonLoader.LoadBaseSeason(saveGame.CurrentSeason.OriginalYear ?? saveGame.CurrentSeason.Year).DeepClone();
            sameSeasonAgain.Year = nextSeasonYear;
            return new MarketOutlookText(saveGame, sameSeasonAgain, new Dictionary<string, IDriverData>());
        }

        public MarketOutlook Estimate(IMarketOutlookEstimator estimator) => estimator.Estimate(_saveGame, NextSeason, NextSeasonDrivers);

        public int RacesToGo()
        {
            var year = _saveGame.CurrentSeason.Year;
            var racesRun = (_saveGame.GrandPrixResults ?? Enumerable.Empty<GrandPrixResult>()).Count(g => g.Year == year);
            return _saveGame.CurrentSeason.Races.Count() - racesRun;
        }

        // what the player's own team decides about them, or what their situation is without one
        public (string Title, string Text) PlayerVerdict(MarketOutlook outlook)
        {
            var team = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == outlook.PlayerTeamId);
            if (team == null)
            {
                // a reserve's deal lasts one season: the team's letter comes over the winter
                var reserveTeam = _saveGame.CurrentSeason.ReserveTeamOf(_saveGame.PlayerData.DriverId);
                return reserveTeam != null
                    ? (string.Format(Strings.MarketOutlookWindow_ReserveTitle_Format, reserveTeam.TeamName), Strings.MarketOutlookWindow_ReserveText)
                    : (Strings.MarketOutlookWindow_NoTeamTitle, Strings.MarketOutlookWindow_NoTeamText);
            }

            switch (outlook.PlayerContractOutcome)
            {
                case DriverFirerOutcome.NOT_DROPPED:
                    // any races left after this season mean a whole season more: contracts are fitted to
                    // the calendar when a season starts
                    return (string.Format(Strings.MarketOutlookWindow_KeptTitle, team.TeamName), Strings.MarketOutlookWindow_KeptText);
                case DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED:
                    return (Strings.MarketOutlookWindow_ContractEndsTitle, Strings.MarketOutlookWindow_ContractEndsText);
                default:
                    return (string.Format(Strings.MarketOutlookWindow_DroppedTitle, team.TeamName), outlook.PlayerContractOutcome switch
                    {
                        DriverFirerOutcome.DROPPED_DISCIPLINARY => Strings.MarketOutlookWindow_DroppedDisciplinaryText,
                        DriverFirerOutcome.DROPPED_TEAM_QUITTING => Strings.MarketOutlookWindow_DroppedTeamQuittingText,
                        _ => Strings.MarketOutlookWindow_DroppedUnderperformingText
                    });
            }
        }

        public string OpeningReason(SeatOutlook seat)
        {
            // reserve deals last one season: whether the team runs one is decided over the winter
            if (seat.IsReserve)
                return seat.ExitingDriverId == null
                    ? Strings.MarketOutlookWindow_Reason_Reserve
                    : string.Format(Strings.MarketOutlookWindow_Reason_ReserveIncumbent_Format, DriverName(seat.ExitingDriverId));

            if (seat.ExitingDriverId == null)
                return Strings.MarketOutlookWindow_Reason_NewTeam;

            var exiting = DriverName(seat.ExitingDriverId);
            return seat.ExitingDriverOutcome switch
            {
                DriverFirerOutcome.NOT_DROPPED => string.Format(Strings.MarketOutlookWindow_Reason_Retiring, exiting),
                DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED => string.Format(Strings.MarketOutlookWindow_Reason_ContractEnds, exiting),
                _ => string.Format(Strings.MarketOutlookWindow_Reason_Dropped, exiting)
            };
        }

        // how the player fits the seat, and who they're up against
        public string CompetitionReason(SeatOutlook seat)
        {
            var fit = FitName(seat.PlayerFit);

            if (seat.BetterFitRivals.Count > 0)
            {
                var format = seat.BetterFitRivals.Count == 1 ? Strings.MarketOutlookWindow_Reason_BetterFitOne : Strings.MarketOutlookWindow_Reason_BetterFitMany;
                return string.Format(format, fit, NameList(seat.BetterFitRivals));
            }

            if (seat.SameFitRivals.Count == 0)
                return string.Format(Strings.MarketOutlookWindow_Reason_NoRivals, fit);

            // the same tier as an under-qualified player is no fit at all
            return seat.PlayerFit == DriverHirer.DriverPolicyFit.UnderQualified
                ? string.Format(Strings.MarketOutlookWindow_Reason_NobodyFits, NameList(seat.SameFitRivals))
                : string.Format(Strings.MarketOutlookWindow_Reason_SameFit, fit, NameList(seat.SameFitRivals));
        }

        // among rivals who fit as well as the player, the bigger name is likelier to get the seat; empty
        // when a better fit takes it anyway, or nobody fits as well
        public string StandingReason(SeatOutlook seat)
        {
            if (seat.BetterFitRivals.Count > 0 || seat.SameFitRivals.Count == 0)
                return "";

            if (seat.StrongerSameFitRivals.Count > 0)
            {
                var format = seat.StrongerSameFitRivals.Count == 1 ? Strings.MarketOutlookWindow_Reason_StrongerOne : Strings.MarketOutlookWindow_Reason_StrongerMany;
                return string.Format(format, NameList(seat.StrongerSameFitRivals));
            }

            if (seat.EqualSameFitRivals.Count > 0)
            {
                var format = seat.EqualSameFitRivals.Count == 1 ? Strings.MarketOutlookWindow_Reason_EqualOne : Strings.MarketOutlookWindow_Reason_EqualMany;
                return string.Format(format, NameList(seat.EqualSameFitRivals));
            }

            return Strings.MarketOutlookWindow_Reason_Weaker;
        }

        // a seat the player would likely get, held down by how seldom it opens (a reserve seat at a team that
        // seldom runs one, a driver who may not retire); empty when the opening doesn't change the band
        public static string OpeningCaveat(SeatOutlook seat)
        {
            var bandIfOpen = MarketOutlookEstimator.ToBand(seat.WinChanceIfOpen);
            return bandIfOpen > seat.Band
                ? string.Format(Strings.MarketOutlookWindow_Reason_OpeningCaveat_Format, BandName(bandIfOpen))
                : "";
        }

        public static string FitName(DriverHirer.DriverPolicyFit fit) => fit switch
        {
            DriverHirer.DriverPolicyFit.OverQualified => Strings.MarketOutlookWindow_Fit_OverQualified,
            DriverHirer.DriverPolicyFit.PerfectFit => Strings.MarketOutlookWindow_Fit_Perfect,
            DriverHirer.DriverPolicyFit.GoodFit => Strings.MarketOutlookWindow_Fit_Good,
            _ => Strings.MarketOutlookWindow_Fit_UnderQualified
        };

        public string NameList(List<string> driverIds)
        {
            var names = string.Join(", ", driverIds.Take(NamedRivals).Select(DriverName));
            return driverIds.Count > NamedRivals ? string.Format(Strings.MarketOutlookWindow_MoreNames, names, driverIds.Count - NamedRivals) : names;
        }

        public IDriverData Driver(string driverId) => _drivers.GetValueOrDefault(driverId);

        public string DriverName(string driverId) => Driver(driverId)?.Name ?? driverId;

        public ITeamEntry Team(string teamId) => _teams.GetValueOrDefault(teamId);

        public string TeamName(string teamId) => Team(teamId)?.TeamName ?? teamId;

        public static string SeatName(SeatOutlook seat) =>
            seat.IsReserve ? Strings.TeamApplicationWindow_ReserveRole
            : seat.Role == DriverRole.FIRST_DRIVER ? Strings.TeamApplicationWindow_FirstDriverRole : Strings.TeamApplicationWindow_SecondDriverRole;

        public static string BandName(MarketChance band) => band switch
        {
            MarketChance.VeryLikely => Strings.MarketOutlookWindow_Band_VeryLikely,
            MarketChance.Likely => Strings.MarketOutlookWindow_Band_Likely,
            MarketChance.Possible => Strings.MarketOutlookWindow_Band_Possible,
            _ => Strings.MarketOutlookWindow_Band_LongShot
        };
    }
}
