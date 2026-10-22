using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
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

        private readonly ISaveGame _saveGame;
        private readonly MarketOutlookText _text;

        public MarketOutlookWindow(ISaveGame saveGame, IGameDataFactory dataFactory, IMarketOutlookEstimator estimator)
            : this(saveGame, MarketOutlookText.Create(saveGame, dataFactory), estimator)
        {
        }

        private MarketOutlookWindow(ISaveGame saveGame, MarketOutlookText text, IMarketOutlookEstimator estimator)
            : this(saveGame, text, text.Estimate(estimator))
        {
        }

        // an outlook already worked out (AgentTalkWindow), so the estimator isn't run again
        public MarketOutlookWindow(ISaveGame saveGame, MarketOutlookText text, MarketOutlook outlook)
        {
            InitializeComponent();

            _saveGame = saveGame;
            _text = text;

            KickerText.Text = GetKicker();
            ShowPlayer(outlook, text.NextSeason.Year);
            ShowSeats(outlook);
        }

        private string GetKicker()
        {
            var year = _saveGame.CurrentSeason.Year;
            var racesRun = (_saveGame.GrandPrixResults ?? Enumerable.Empty<GrandPrixResult>()).Where(g => g.Year == year).ToList();
            var racesToGo = _text.RacesToGo();

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
            var player = _text.Driver(playerId);
            // this season's team: it holds the player's contract
            var team = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == outlook.PlayerTeamId);
            var contract = team == null ? null : new[] { team.Driver1Contract, team.Driver2Contract }.FirstOrDefault(c => c?.DriverId == playerId);
            var reserveTeam = team == null ? _saveGame.CurrentSeason.ReserveTeamOf(playerId) : null;

            DriverNameText.Text = player?.Name ?? _saveGame.PlayerData.Name;
            DriverDetailText.Text = team != null
                ? string.Format(Strings.MarketOutlookWindow_DriverDetail, team.TeamName, TeamSelectionWindow.GetRoleName(contract.Role), nextSeasonYear - (player?.YearOfBirth ?? 0))
                : reserveTeam != null
                    ? string.Format(Strings.MarketOutlookWindow_ReserveDetail_Format, reserveTeam.TeamName)
                    : Strings.MarketOutlookWindow_NoTeamTitle;

            var position = _saveGame.CurrentDriverStandings?.FirstOrDefault(s => s.DriverId == playerId)?.Position;
            ChampionshipText.Text = position.HasValue ? $"P{position}" : "–";
            ReputationLabelText.Text = string.Format(Strings.MarketOutlookWindow_ReputationLabel, nextSeasonYear);
            ReputationText.Text = SeasonOverviewWindow.FormatReputation(outlook.PlayerReputation);

            (PostItTitleText.Text, PostItText.Text) = _text.PlayerVerdict(outlook);
        }

        private void ShowSeats(MarketOutlook outlook)
        {
            SeatsList.ItemsSource = outlook.Seats.Select(s => new SeatRow
            {
                TeamName = _text.TeamName(s.TeamId),
                TeamColor = HeadToHeadWindow.ToBrush(_text.Team(s.TeamId)?.Color),
                Detail = string.Format(Strings.MarketOutlookWindow_SeatDetail, MarketOutlookText.SeatName(s), TeamApplicationWindow.GetReputationLabel(s.TeamReputation)),
                BandText = MarketOutlookText.BandName(s.Band),
                BandBrush = BandBrush(s.Band),
                Reason = string.Join(" ", new[] { _text.OpeningReason(s), _text.CompetitionReason(s), _text.StandingReason(s), MarketOutlookText.OpeningCaveat(s) }.Where(r => r.Length > 0))
            }).ToList();

            var anySeat = outlook.Seats.Count > 0;
            NoSeatsText.Visibility = anySeat ? Visibility.Collapsed : Visibility.Visible;
            BestBox.Visibility = anySeat ? Visibility.Visible : Visibility.Collapsed;
            if (!anySeat)
                return;

            BestHeaderText.Text = string.Format(Strings.MarketOutlookWindow_BestHeader, outlook.BestApplications.Count);
            BestText.Text = string.Join(" · ", outlook.BestApplications.Select(s => $"{_text.TeamName(s.TeamId)} ({MarketOutlookText.SeatName(s).ToLower()})"));
            HiredStampText.Text = string.Format(Strings.MarketOutlookWindow_HiredStamp, MarketOutlookText.BandName(outlook.HiredBand));
            HiredStampText.Foreground = HiredStamp.BorderBrush = BandBrush(outlook.HiredBand);
        }

        private Brush BandBrush(MarketChance band) => (Brush)FindResource($"MarketOutlook.{band}Brush");
    }
}
