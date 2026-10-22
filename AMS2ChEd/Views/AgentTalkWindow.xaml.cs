using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Resources;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The driver market outlook (IMarketOutlookEstimator) as a conversation with the player's agent: the
    /// player picks a question, the agent answers, then asks whether there's anything else. The full
    /// report is MarketOutlookWindow, opened on the same outlook.
    /// </summary>
    public partial class AgentTalkWindow : Window
    {
        private class AgentMessage
        {
            public string Text { get; init; }
            public bool IsPlayer { get; init; }
        }

        private class AgentOption
        {
            public string Label { get; init; }
            public Action Pick { get; init; }
        }

        // the agent's frames change every half second
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(0.5);

        // the mouth opens on every other tick of a reply's first six: three times
        private const int TalkTicks = 6;

        // the eyes look left for one tick, right for one, then straight for four
        private const int EyeCycleTicks = 6;

        private readonly ISaveGame _saveGame;
        private readonly MarketOutlookText _text;
        private readonly MarketOutlook _outlook;
        private readonly ObservableCollection<AgentMessage> _messages = new();
        private readonly DispatcherTimer _timer;
        private int _tick;
        private int _talk;

        public AgentTalkWindow(ISaveGame saveGame, IGameDataFactory dataFactory, IMarketOutlookEstimator estimator)
        {
            InitializeComponent();

            _saveGame = saveGame;
            _text = MarketOutlookText.Create(saveGame, dataFactory);
            _outlook = _text.Estimate(estimator);

            MessagesList.ItemsSource = _messages;
            ContactDetailText.Text = GetContactDetail();
            // nothing can change any more once the season is over
            CautionText.Visibility = _text.RacesToGo() > 0 ? Visibility.Visible : Visibility.Collapsed;

            _timer = new DispatcherTimer { Interval = TickInterval };
            _timer.Tick += (_, _) =>
            {
                _tick++;
                _talk = Math.Max(0, _talk - 1);
                ShowAgent();
            };
            _timer.Start();
            Closed += (_, _) => _timer.Stop();

            Say(Strings.AgentTalkWindow_Greeting);
            ShowQuestions();
        }

        private ITeamEntry OwnTeam => _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == _outlook.PlayerTeamId);

        private ITeamEntry ReserveTeam => OwnTeam == null ? _saveGame.CurrentSeason.ReserveTeamOf(_saveGame.PlayerData.DriverId) : null;

        private string GetContactDetail()
        {
            var name = _text.Driver(_saveGame.PlayerData.DriverId)?.Name ?? _saveGame.PlayerData.Name;
            var situation = OwnTeam?.TeamName
                ?? (ReserveTeam != null ? string.Format(Strings.MarketOutlookWindow_ReserveDetail_Format, ReserveTeam.TeamName) : Strings.MarketOutlookWindow_NoTeamTitle);
            return $"{name} · {situation}";
        }

        private void ShowAgent()
        {
            var phase = _tick % EyeCycleTicks;
            var eyes = phase == 0 ? AgentSprite.Eyes.Left : phase == 1 ? AgentSprite.Eyes.Right : AgentSprite.Eyes.Straight;
            AgentImage.Source = AgentSprite.Frame(eyes, _talk > 0 && _talk % 2 == 0);

            // the cursor at the end of the bubble blinks; hidden by colour so the text doesn't reflow
            if (_tick % 2 == 0)
                CursorRun.ClearValue(TextElement.ForegroundProperty);
            else
                CursorRun.Foreground = Brushes.Transparent;
        }

        // the agent's replies: one message each, all of them together in the bubble
        private void Say(params string[] replies)
        {
            foreach (var reply in replies)
                _messages.Add(new AgentMessage { Text = reply });

            BubbleRun.Text = string.Join("\n\n", replies);
            ScrollBubbleToTop();
            _talk = TalkTicks;
            ShowAgent();
            Dispatcher.BeginInvoke(ThreadScroll.ScrollToEnd, DispatcherPriority.Background);
        }

        // a style may wrap the bubble's text in a scroller (the retro one does): a new reply starts at its top
        private void ScrollBubbleToTop()
        {
            for (DependencyObject node = BubbleText; node != null && node != BubbleControl; node = VisualTreeHelper.GetParent(node))
            {
                if (node is ScrollViewer scroller)
                {
                    scroller.ScrollToTop();
                    return;
                }
            }
        }

        private void Option_Click(object sender, RoutedEventArgs e)
        {
            var option = (AgentOption)((FrameworkElement)sender).DataContext;
            _messages.Add(new AgentMessage { Text = option.Label, IsPlayer = true });
            option.Pick();
        }

        private void ShowQuestions()
        {
            var options = new List<AgentOption>();

            var ownTeam = OwnTeam;
            if (ownTeam != null)
                options.Add(new AgentOption { Label = string.Format(Strings.AgentTalkWindow_Ask_OwnTeam_Format, ownTeam.TeamName), Pick = AnswerOwnTeam });

            var reserveTeam = ReserveTeam;
            if (reserveTeam != null)
                options.Add(new AgentOption { Label = string.Format(Strings.AgentTalkWindow_Ask_Promotion_Format, reserveTeam.TeamName), Pick = () => AnswerPromotion(reserveTeam) });

            options.Add(new AgentOption { Label = Strings.AgentTalkWindow_Ask_AnySeat, Pick = AnswerAnySeat });
            options.Add(new AgentOption { Label = Strings.AgentTalkWindow_Ask_FullReport, Pick = ShowFullReport });
            options.Add(new AgentOption { Label = Strings.AgentTalkWindow_Ask_Reputation, Pick = AnswerReputation });
            options.Add(new AgentOption { Label = Strings.AgentTalkWindow_Ask_Team, Pick = AskWhichTeam });

            OptionsList.ItemsSource = options;
        }

        private void AnswerOwnTeam()
        {
            var (title, text) = _text.PlayerVerdict(_outlook);
            Say(title + "\n" + text, Strings.AgentTalkWindow_AnythingElse);
        }

        // a reserve is promoted when a race seat opens that they're a PerfectFit for (first refusal)
        private void AnswerPromotion(ITeamEntry reserveTeam)
        {
            var promotion = _outlook.Seats.Any(s => s.TeamId == reserveTeam.TeamId && !s.IsReserve && s.PlayerFit == DriverHirer.DriverPolicyFit.PerfectFit);
            var format = promotion ? Strings.AgentTalkWindow_PromotionYes_Format : Strings.AgentTalkWindow_PromotionNo_Format;
            Say(string.Format(format, reserveTeam.TeamName), Strings.AgentTalkWindow_AnythingElse);
        }

        private void AnswerAnySeat()
        {
            if (_outlook.Seats.Count == 0)
            {
                Say(Strings.MarketOutlookWindow_NoSeats, Strings.AgentTalkWindow_AnythingElse);
                return;
            }

            var bestShots = string.Join(", ", _outlook.BestApplications.Select(s => $"{_text.TeamName(s.TeamId)} ({MarketOutlookText.SeatName(s).ToLower()})"));
            Say(string.Format(Strings.AgentTalkWindow_Overall_Format, MarketOutlookText.BandName(_outlook.HiredBand), bestShots), Strings.AgentTalkWindow_AnythingElse);
        }

        private void ShowFullReport()
        {
            Say(Strings.AgentTalkWindow_FullReport);
            // once the reply is on screen
            Dispatcher.BeginInvoke(() =>
            {
                var marketOutlookWindow = new MarketOutlookWindow(_saveGame, _text, _outlook);
                marketOutlookWindow.Owner = this;
                marketOutlookWindow.ShowDialog();
                Say(Strings.AgentTalkWindow_AnythingElse);
            }, DispatcherPriority.Background);
        }

        private void AnswerReputation()
        {
            Say(string.Format(Strings.AgentTalkWindow_Reputation_Format, SeasonOverviewWindow.FormatReputation(_outlook.PlayerReputation)), Strings.AgentTalkWindow_AnythingElse);
        }

        // the teams with a seat that may open, most likely first
        private void AskWhichTeam()
        {
            if (_outlook.Seats.Count == 0)
            {
                Say(Strings.MarketOutlookWindow_NoSeats, Strings.AgentTalkWindow_AnythingElse);
                return;
            }

            Say(Strings.AgentTalkWindow_WhichTeam);

            var options = _outlook.Seats
                .Select(s => s.TeamId)
                .Distinct()
                .Select(teamId => new AgentOption { Label = _text.TeamName(teamId), Pick = () => AnswerTeam(teamId) })
                .ToList();
            options.Add(new AgentOption
            {
                Label = Strings.AgentTalkWindow_NeverMind,
                Pick = () =>
                {
                    Say(Strings.AgentTalkWindow_AnythingElse);
                    ShowQuestions();
                }
            });
            OptionsList.ItemsSource = options;
        }

        // one message per seat that may open at the team: the race seats in order, then the reserve
        private void AnswerTeam(string teamId)
        {
            var seats = _outlook.Seats.Where(s => s.TeamId == teamId).OrderBy(s => s.IsReserve).ThenBy(s => s.Slot).ToList();
            Say(seats.Select((seat, i) => SeatMessage(seat, i == 0)).Append(Strings.AgentTalkWindow_AnythingElse).ToArray());
            ShowQuestions();
        }

        private string SeatMessage(SeatOutlook seat, bool isFirst)
        {
            var role = seat.IsReserve ? Strings.AgentTalkWindow_Role_Reserve
                : seat.Role == DriverRole.FIRST_DRIVER ? Strings.AgentTalkWindow_Role_First : Strings.AgentTalkWindow_Role_Second;
            var lookingFor = isFirst
                ? string.Format(Strings.AgentTalkWindow_Seat_First_Format, _text.TeamName(seat.TeamId), role, Opening(seat))
                : string.Format(Strings.AgentTalkWindow_Seat_More_Format, role, Opening(seat));
            var fit = string.Format(Strings.AgentTalkWindow_Fit_Format, MarketOutlookText.FitName(seat.PlayerFit), Rivals(seat));
            var standing = _text.StandingReason(seat);
            var caveat = MarketOutlookText.OpeningCaveat(seat);
            return lookingFor + "\n" + Chance(seat.Band) + (caveat.Length > 0 ? " " + caveat : "") + "\n" + fit + (standing.Length > 0 ? " " + standing : "");
        }

        // why the seat may open, as a clause ("as ... ") following the role
        private string Opening(SeatOutlook seat)
        {
            if (seat.IsReserve)
                return seat.ExitingDriverId == null
                    ? Strings.AgentTalkWindow_Opening_Reserve
                    : string.Format(Strings.AgentTalkWindow_Opening_ReserveIncumbent_Format, _text.DriverName(seat.ExitingDriverId));

            if (seat.ExitingDriverId == null)
                return Strings.AgentTalkWindow_Opening_NewTeam;

            var exiting = _text.DriverName(seat.ExitingDriverId);
            return seat.ExitingDriverOutcome switch
            {
                DriverFirerOutcome.NOT_DROPPED => string.Format(Strings.AgentTalkWindow_Opening_Retiring_Format, exiting),
                DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED => string.Format(Strings.AgentTalkWindow_Opening_ContractEnds_Format, exiting),
                _ => string.Format(Strings.AgentTalkWindow_Opening_Dropped_Format, exiting)
            };
        }

        private static string Chance(MarketChance band) => band switch
        {
            MarketChance.VeryLikely => Strings.AgentTalkWindow_Chance_VeryLikely,
            MarketChance.Likely => Strings.AgentTalkWindow_Chance_Likely,
            MarketChance.Possible => Strings.AgentTalkWindow_Chance_Possible,
            _ => Strings.AgentTalkWindow_Chance_LongShot
        };

        // who the player is up against, as MarketOutlookText.CompetitionReason without the player's fit
        private string Rivals(SeatOutlook seat)
        {
            if (seat.BetterFitRivals.Count > 0)
            {
                var format = seat.BetterFitRivals.Count == 1 ? Strings.AgentTalkWindow_Rivals_BetterFitOne_Format : Strings.AgentTalkWindow_Rivals_BetterFitMany_Format;
                return string.Format(format, _text.NameList(seat.BetterFitRivals));
            }

            if (seat.SameFitRivals.Count == 0)
                return Strings.AgentTalkWindow_Rivals_None;

            // the same tier as an under-qualified player is no fit at all
            var one = seat.SameFitRivals.Count == 1;
            var sameFitFormat = seat.PlayerFit == DriverHirer.DriverPolicyFit.UnderQualified
                ? (one ? Strings.AgentTalkWindow_Rivals_NobodyFitsOne_Format : Strings.AgentTalkWindow_Rivals_NobodyFitsMany_Format)
                : (one ? Strings.AgentTalkWindow_Rivals_SameFitOne_Format : Strings.AgentTalkWindow_Rivals_SameFitMany_Format);
            return string.Format(sameFitFormat, _text.NameList(seat.SameFitRivals));
        }
    }
}
