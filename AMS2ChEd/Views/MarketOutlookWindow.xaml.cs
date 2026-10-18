using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Resources;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// What the driver market would offer the player if the season ended today (IMarketOutlookEstimator):
    /// the seats that may open, the player's chance at each and why, and what their own team decides.
    /// </summary>
    public partial class MarketOutlookWindow : Window
    {
        private class SeatRow
        {
            public string TeamName { get; set; }
            public Brush TeamColor { get; set; }
            public string Detail { get; set; }
            public string BandText { get; set; }
            public Brush BandBrush { get; set; }
            public string Reason { get; set; }
        }

        // rivals named in a seat's reason before the rest are counted
        private const int NamedRivals = 3;

        private readonly ISaveGame _saveGame;
        private readonly Dictionary<string, IDriverData> _drivers;
        private readonly Dictionary<string, ITeamEntry> _teams;

        public MarketOutlookWindow(ISaveGame saveGame, IGameDataFactory dataFactory, IMarketOutlookEstimator estimator)
        {
            InitializeComponent();

            _saveGame = saveGame;
            var (nextSeason, nextSeasonDrivers) = LoadNextSeason(saveGame, dataFactory);

            _drivers = nextSeasonDrivers.Values
                .Concat(saveGame.Drivers ?? Enumerable.Empty<IDriverData>())
                .Concat(saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .GroupBy(d => d.DriverId)
                .ToDictionary(g => g.Key, g => g.Last());
            _teams = saveGame.CurrentSeason.Teams
                .Concat(nextSeason.Teams)
                .GroupBy(t => t.TeamId)
                .ToDictionary(g => g.Key, g => g.Last());

            var outlook = estimator.Estimate(saveGame, nextSeason, nextSeasonDrivers);

            KickerText.Text = GetKicker();
            ShowPlayer(outlook, nextSeason.Year);
            ShowSeats(outlook);
        }

        // next season's pack, or this season's again when it isn't installed (as the off-season does)
        private static (ISeason, IReadOnlyDictionary<string, IDriverData>) LoadNextSeason(ISaveGame saveGame, IGameDataFactory dataFactory)
        {
            var nextSeasonYear = saveGame.CurrentSeason.Year + 1;
            if (dataFactory.SeasonLoader.GetAvailableSeasons().Contains(nextSeasonYear.ToString()))
            {
                return (dataFactory.SeasonLoader.LoadBaseSeason(nextSeasonYear), dataFactory.DriversLoader.LoadDriversBase(nextSeasonYear));
            }

            var sameSeasonAgain = dataFactory.SeasonLoader.LoadBaseSeason(saveGame.CurrentSeason.OriginalYear ?? saveGame.CurrentSeason.Year).DeepClone();
            sameSeasonAgain.Year = nextSeasonYear;
            return (sameSeasonAgain, new Dictionary<string, IDriverData>());
        }

        private string GetKicker()
        {
            var year = _saveGame.CurrentSeason.Year;
            var racesRun = (_saveGame.GrandPrixResults ?? Enumerable.Empty<GrandPrixResult>()).Where(g => g.Year == year).ToList();
            var racesToGo = _saveGame.CurrentSeason.Races.Count() - racesRun.Count;

            var kicker = racesRun.Count == 0
                ? string.Format(Strings.MarketOutlookWindow_KickerBeforeSeason, year)
                : string.Format(Strings.MarketOutlookWindow_KickerAfterRace, year, racesRun.Last().GrandPrixName?.ToUpper());
            if (racesToGo == 1)
                kicker += " · " + Strings.MarketOutlookWindow_RacesToGoOne;
            else if (racesToGo > 1)
                kicker += " · " + string.Format(Strings.MarketOutlookWindow_RacesToGoMany, racesToGo);
            return kicker;
        }

        private void ShowPlayer(MarketOutlook outlook, int nextSeasonYear)
        {
            var playerId = _saveGame.PlayerData.DriverId;
            var player = _drivers.GetValueOrDefault(playerId);
            // this season's team: it holds the player's contract
            var team = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == outlook.PlayerTeamId);
            var contract = team == null ? null : new[] { team.Driver1Contract, team.Driver2Contract }.FirstOrDefault(c => c?.DriverId == playerId);

            DriverNameText.Text = player?.Name ?? _saveGame.PlayerData.Name;
            DriverDetailText.Text = team == null
                ? Strings.MarketOutlookWindow_NoTeamTitle
                : string.Format(Strings.MarketOutlookWindow_DriverDetail, team.TeamName, TeamSelectionWindow.GetRoleName(contract.Role), nextSeasonYear - (player?.YearOfBirth ?? 0));

            var position = _saveGame.CurrentDriverStandings?.FirstOrDefault(s => s.DriverId == playerId)?.Position;
            ChampionshipText.Text = position.HasValue ? $"P{position}" : "–";
            ReputationLabelText.Text = string.Format(Strings.MarketOutlookWindow_ReputationLabel, nextSeasonYear);
            ReputationText.Text = SeasonOverviewWindow.FormatReputation(outlook.PlayerReputation);

            if (team == null)
            {
                PostItTitleText.Text = Strings.MarketOutlookWindow_NoTeamTitle;
                PostItText.Text = Strings.MarketOutlookWindow_NoTeamText;
                return;
            }

            switch (outlook.PlayerContractOutcome)
            {
                case DriverFirerOutcome.NOT_DROPPED:
                    // any races left after this season mean a whole season more: contracts are fitted to
                    // the calendar when a season starts
                    PostItTitleText.Text = string.Format(Strings.MarketOutlookWindow_KeptTitle, team.TeamName);
                    PostItText.Text = Strings.MarketOutlookWindow_KeptText;
                    break;
                case DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED:
                    PostItTitleText.Text = Strings.MarketOutlookWindow_ContractEndsTitle;
                    PostItText.Text = Strings.MarketOutlookWindow_ContractEndsText;
                    break;
                default:
                    PostItTitleText.Text = string.Format(Strings.MarketOutlookWindow_DroppedTitle, team.TeamName);
                    PostItText.Text = outlook.PlayerContractOutcome switch
                    {
                        DriverFirerOutcome.DROPPED_DISCIPLINARY => Strings.MarketOutlookWindow_DroppedDisciplinaryText,
                        DriverFirerOutcome.DROPPED_TEAM_QUITTING => Strings.MarketOutlookWindow_DroppedTeamQuittingText,
                        _ => Strings.MarketOutlookWindow_DroppedUnderperformingText
                    };
                    break;
            }
        }

        private void ShowSeats(MarketOutlook outlook)
        {
            SeatsList.ItemsSource = outlook.Seats.Select(s => new SeatRow
            {
                TeamName = TeamName(s.TeamId),
                TeamColor = HeadToHeadWindow.ToBrush(_teams.GetValueOrDefault(s.TeamId)?.Color),
                Detail = string.Format(Strings.MarketOutlookWindow_SeatDetail, RoleName(s.Role), TeamApplicationWindow.GetReputationLabel(s.TeamReputation)),                BandText = BandName(s.Band),
                BandBrush = BandBrush(s.Band),
                Reason = OpeningReason(s) + " " + CompetitionReason(s)
            }).ToList();

            var anySeat = outlook.Seats.Count > 0;
            NoSeatsText.Visibility = anySeat ? Visibility.Collapsed : Visibility.Visible;
            BestBox.Visibility = anySeat ? Visibility.Visible : Visibility.Collapsed;
            if (!anySeat)
                return;

            BestHeaderText.Text = string.Format(Strings.MarketOutlookWindow_BestHeader, outlook.BestApplications.Count);
            BestText.Text = string.Join(" · ", outlook.BestApplications.Select(s => $"{TeamName(s.TeamId)} ({RoleName(s.Role).ToLower()})"));
            HiredStampText.Text = string.Format(Strings.MarketOutlookWindow_HiredStamp, BandName(outlook.HiredBand));
            HiredStampText.Foreground = HiredStamp.BorderBrush = BandBrush(outlook.HiredBand);
        }

        private string OpeningReason(SeatOutlook seat)
        {
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
        private string CompetitionReason(SeatOutlook seat)
        {
            var fit = seat.PlayerFit switch
            {
                DriverHirer.DriverPolicyFit.OverQualified => Strings.MarketOutlookWindow_Fit_OverQualified,
                DriverHirer.DriverPolicyFit.PerfectFit => Strings.MarketOutlookWindow_Fit_Perfect,
                DriverHirer.DriverPolicyFit.GoodFit => Strings.MarketOutlookWindow_Fit_Good,
                _ => Strings.MarketOutlookWindow_Fit_UnderQualified
            };

            if (seat.BetterFitRivals.Count > 0)
            {
                var format = seat.BetterFitRivals.Count == 1 ? Strings.MarketOutlookWindow_Reason_BetterFitOne : Strings.MarketOutlookWindow_Reason_BetterFitMany;
                return string.Format(format, fit, NameList(seat.BetterFitRivals));
            }

            return seat.SameFitRivals.Count > 0
                ? string.Format(Strings.MarketOutlookWindow_Reason_SameFit, fit, NameList(seat.SameFitRivals))
                : string.Format(Strings.MarketOutlookWindow_Reason_NoRivals, fit);
        }

        private string NameList(List<string> driverIds)
        {
            var names = string.Join(", ", driverIds.Take(NamedRivals).Select(DriverName));
            return driverIds.Count > NamedRivals ? string.Format(Strings.MarketOutlookWindow_MoreNames, names, driverIds.Count - NamedRivals) : names;
        }

        private string DriverName(string driverId) => _drivers.GetValueOrDefault(driverId)?.Name ?? driverId;

        private string TeamName(string teamId) => _teams.GetValueOrDefault(teamId)?.TeamName ?? teamId;

        private static string RoleName(DriverRole role) =>
            role == DriverRole.FIRST_DRIVER ? Strings.TeamApplicationWindow_FirstDriverRole : Strings.TeamApplicationWindow_SecondDriverRole;

        private static string BandName(MarketChance band) => band switch
        {
            MarketChance.VeryLikely => Strings.MarketOutlookWindow_Band_VeryLikely,
            MarketChance.Likely => Strings.MarketOutlookWindow_Band_Likely,
            MarketChance.Possible => Strings.MarketOutlookWindow_Band_Possible,
            _ => Strings.MarketOutlookWindow_Band_LongShot
        };

        private Brush BandBrush(MarketChance band) => (Brush)FindResource($"MarketOutlook.{band}Brush");
    }
}
