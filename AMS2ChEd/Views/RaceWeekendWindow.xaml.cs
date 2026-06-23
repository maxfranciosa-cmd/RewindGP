using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Mocks;
using AMS2ChEd.Business.Services.RaceNumberSystem.Factory;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace AMS2ChEd.Views
{
    public enum GraphicsStyle
    {
        Pre94,
        Nineties1990s,
        Mid2000s,
        Twenties2010s,
        Twenties2020s
    }

    public class StandingEntry
    {
        public int Position { get; set; }
        public int Number { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string Nationality { get; set; }
        public string BestLap { get; set; }
        public Brush LapTimeColor { get; set; }
        public GraphicsStyle Style { get; set; }
        public Brush PositionBackground { get; set; }
        public Brush PositionForeground { get; set; }
        public Brush DriverNameBackground { get; set; }
        public Brush DriverNameForeground { get; set; }
        public Brush TeamNameBackground { get; set; }
        public Brush TeamNameForeground { get; set; }
        public Brush BestLapBackground { get; set; }
        public Brush BestLapForeground { get; set; }
        public bool ShowNumber { get; set; }
        public Visibility ShowNumberColumn { get; set; }
        public Visibility ShowPre94Layout { get; set; }
        public Visibility ShowModernLayout { get; set; }
        public Visibility Show2010sLayout { get; set; }
        public Visibility Show2020sLayout { get; set; }
        public Brush TeamColorAccent { get; set; }
        public Thickness RowMargin { get; set; }
        public Thickness PositionMargin { get; set; }
        public HorizontalAlignment PositionAlignment { get; set; }
        public GridLength PositionWidth { get; set; }
        public double PositionMinWidth { get; set; }
        public double PositionBorderWidth { get; set; } // Use NaN for stretch
        public Thickness DriverNameTextMargin { get; set; }
        public Thickness TeamNameTextMargin { get; set; }
        public Thickness BestLapTextMargin { get; set; }
        public GridLength NatColumnWidth { get; set; }
        public GridLength NumberColumnWidth { get; set; }
        public System.Windows.FontStyle FontStyleValue { get; set; }
    }

    public class UpperCaseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToUpper() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class RaceWeekendWindow : Window
    {
        private ISaveGame saveGame;
        private IRaceDataService _raceDataService;
        private Window _controlWindow;
        private GameLogicFactory _gameLogicFactory;
        private List<ParticipantData> _qualifyingResults;
        private GraphicsStyle _currentStyle = GraphicsStyle.Pre94;
        private readonly bool _preQualiMode;
        private string _originalGrandPrixName = "";
        private string _originalCircuitName = "";

        public event EventHandler<ISaveGame> RaceCompleted;
        public RaceWeekendWindow(GameLogicFactory gameLogicFactory, ISaveGame saveGame, SimulatedRaceDataService simulatedRaceDataService, bool simulateRace, bool preQualiMode = false)
        {
            InitializeComponent();
            this.saveGame = saveGame;
            _preQualiMode = preQualiMode;
            this._raceDataService = simulateRace ? simulatedRaceDataService : gameLogicFactory.RaceDataService;
            _gameLogicFactory = gameLogicFactory;

            // Subscribe to service events
            _raceDataService.SessionUpdated += OnSessionUpdated;
            _raceDataService.SessionFinished += OnSessionFinished;

            // Start the service
            if (!_raceDataService.IsRunning)
            {
                _raceDataService.Start();
            }

            // Open control window if using mock service
            if (_raceDataService is MockUserControlledRaceDataService mockService)
            {
                _controlWindow = new MockRaceControlWindow(mockService, preQualiMode);
                _controlWindow.Show();
            }

            if (!preQualiMode)
            {
                if (simulateRace)
                {
                    // if the race is simulated, we are not at the last race
                    // AND in the next race is not in any absence
                    // i'll ask the player if they want to generate an absence for the next race.
                    if (saveGame.CurrentSeason.Races.Count() > (saveGame.NextGpIndex + 1) && ThereAreNoAbsencesTheNextRaceWhereThePlayerCanFillIn())
                    {
                        var generateAbsenceWindow = new GenerateAbsenceWindow(GenerateAbsenceWindowType.NoTeamBeforeTheRace);
                        generateAbsenceWindow.ShowDialog();

                        if (generateAbsenceWindow.CreateFictionalAbsence)
                        {
                            // if there are no absences at the next GP of the season
                            var nextRaceId = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex + 1).RaceId;
                            if (!saveGame.CurrentSeason.Absences.Any(a => a.RaceId == nextRaceId))
                            {
                                // create a new random absence in a midfield (or lower) team
                                var random = new Random();
                                var possibleTeams = saveGame.CurrentSeason
                                                .Teams
                                                .Where(t => t.Reputation <= TeamReputation.MIDFIELD)
                                                .ToList();

                                var selectedTeam = possibleTeams.ElementAt(random.Next(possibleTeams.Count));

                                var driverOut = selectedTeam.PickRandomDriverFromTheTeam();

                                saveGame.CurrentSeason.Absences = saveGame.CurrentSeason.Absences.Concat(new[]
                                {
                                new Absence
                                {
                                    DriverOut = driverOut.DriverId,
                                    RaceId = nextRaceId,
                                    DriverIn = saveGame.PlayerData.DriverId,
                                    TeamId = selectedTeam.TeamId,
                                }
                            });
                            }
                        }
                    }
                }
                else
                {
                    var playerTeam = saveGame.NextGpEntryList.FirstOrDefault(e => new[] { e.Driver1Id, e.Driver2Id }.Contains(saveGame.PlayerData.DriverId));
                    var team = string.IsNullOrEmpty(playerTeam?.TeamId) ? null : saveGame.CurrentSeason.Teams.Where(t => t.TeamId == playerTeam?.TeamId)?.Cast<Ams2TeamEntry>().FirstOrDefault();
                    var playerDriverSlot = playerTeam.Driver1Id == saveGame.PlayerData.DriverId ? 1 : 2;
                    var playerNumber = playerDriverSlot == 1 ? playerTeam.Driver1Number : playerTeam.Driver2Number;
                    var numberofOpponents = (saveGame.NextGpEntryList.Sum(e => (string.IsNullOrEmpty(e.Driver1Id) ? 0 : 1) + (string.IsNullOrEmpty(e.Driver2Id) ? 0 : 1))) - 1;
                    var difficultyDelta = CalculateDifficulty(team);
                    var raceInstructionsWindow = new RaceInstructionsWindow(saveGame.PlayerData.Name, team?.GetAms2Car(playerDriverSlot) ?? "", $"#{playerNumber} {team?.TeamName} - {saveGame.PlayerData.Name}", numberofOpponents, difficultyDelta);
                    raceInstructionsWindow.ShowDialog();
                }
            }


            // Apply initial style colors
            _currentStyle = SelectStyle(saveGame.CurrentSeason.Year);

            LoadRaceWeekend();

            UpdateHeadersForStyle();

            UpdateUIColorsForStyle();

            if (preQualiMode)
            {
                SessionLabelText.Text = "PRE-QUALIFYING:";
                SessionText.Text = "QUALIFYING SESSION";
            }
        }

        private bool ThereAreNoAbsencesTheNextRaceWhereThePlayerCanFillIn()
        {
            var absencesForTheNextRace = saveGame.CurrentSeason.Absences.Where(a => a.RaceId == saveGame.NextGpIndex).ToList();
            return _gameLogicFactory.AbsenceManager.IsDriverInAnyAbsence(saveGame.PlayerData.DriverId, absencesForTheNextRace);
        }

        private GraphicsStyle SelectStyle(int year)
        {
            if (year < 1994) return GraphicsStyle.Pre94;
            if (year < 2005) return GraphicsStyle.Nineties1990s;
            if (year < 2010) return GraphicsStyle.Mid2000s;
            if (year < 2018) return GraphicsStyle.Twenties2010s;
            return GraphicsStyle.Twenties2020s;
        }

        private int CalculateDifficulty(Ams2TeamEntry? team)
        {
            if (team == null) return 0;

            switch (team.Reputation)
            {
                case TeamReputation.SUPER_MINNOW:
                    return 15;
                case TeamReputation.MINNOW:
                    return 10;
                case TeamReputation.MIDFIELD:
                    return 7;
                case TeamReputation.MIDFIELD_HIGH:
                    return 5;
                case TeamReputation.TOP_TEAM:
                    return 0;
                default:
                    return 0;
            }
        }


        private void OnSessionUpdated(object sender, SessionUpdateEventArgs e)
        {
            // Update UI on the dispatcher thread
            Dispatcher.Invoke(() =>
            {
                UpdateSessionDisplay(e.SessionData);
            });
        }

        private void OnSessionFinished(object sender, SessionFinishedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_preQualiMode)
                {
                    // In pre-qualifying mode only update the display for qualifying.
                    // All other session types (practice, race) are silently dropped.
                    // Championship handling and RaceCompleted are always blocked.
                    if (e.CompletedSession == SessionType.Qualification)
                    {
                        UpdateSessionDisplay(new SessionData
                        {
                            SessionType = SessionType.Qualification,
                            IsSessionFinished = true,
                            Standings = e.FinalStandings
                        });
                    }
                    return;
                }

                // Store session results based on type
                switch (e.CompletedSession)
                {
                    case SessionType.Qualification:
                        // Store qualifying results
                        _qualifyingResults = e.FinalStandings;

                        break;

                    case SessionType.Race:
                        // Create comprehensive race result
                        var raceResult = CreateCompleteGrandPrixResult(e.FinalStandings);

                        // Merge DNPQ records into the result for historical completeness.
                        // These are excluded from standings by the DidNotPreQualify guard above.
                        if (saveGame.CurrentPreQualiDnpqResults?.Any() == true)
                        {
                            raceResult.RaceResults = raceResult.RaceResults
                                .Concat(ConvertParticipantsToDriverResults_IncludingDnpq(
                                    saveGame.CurrentPreQualiDnpqResults))
                                .ToList();
                        }

                        // Get winner's PREVIOUS standing position (before update)
                        var winner = raceResult.RaceResults.FirstOrDefault(d => d.Position == 1);
                        int previousWinnerPosition = 1;
                        if (winner != null)
                        {
                            var winnerStanding = saveGame.CurrentDriverStandings
                                .FirstOrDefault(s => s.DriverId == winner.DriverId);
                            previousWinnerPosition = winnerStanding?.Position ?? 1;
                        }
                        // Update standings
                        _gameLogicFactory.StandingsManager.UpdateStandings(saveGame, raceResult);

                        // Add race result to history
                        saveGame.GrandPrixResults = saveGame.GrandPrixResults.Append(raceResult);

                        var gpDate = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex).RaceDate;

                        // Increment to next GP
                        saveGame.NextGpIndex++;
                        saveGame.NextGpEntryList = null;

                        // Stop the race data service to prevent further polling
                        if (_raceDataService.IsRunning)
                        {
                            _raceDataService.Stop();
                        }

                        // Show post-race newspaper
                        var winnerDriverData = saveGame.Drivers.FirstOrDefault(d => d.DriverId == winner.DriverId);
                        var winnerPhoto = winnerDriverData?.PictureUrl;
                        var newsWindow = new PostRaceNewsWindow(saveGame, raceResult, previousWinnerPosition, DateTime.ParseExact(gpDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), winnerPhoto);
                        newsWindow.Owner = this;
                        newsWindow.ShowDialog();

                        // Keep the race results visible after stopping the service
                        Dispatcher.Invoke(() =>
                        {
                            WaitingMessage.Visibility = Visibility.Collapsed;
                            StandingsContent.Visibility = Visibility.Visible;
                        });


                        // Fire event with updated saveGame
                        RaceCompleted?.Invoke(this, saveGame);
                        break;
                }
            });
        }
        private List<SessionResult> ConvertParticipantsToDriverResults_IncludingDnpq(List<ParticipantData> participants)
        {
            return participants.Select(p => new SessionResult
            {
                DriverId = p.DriverId,
                TeamId = p.TeamId,
                Position = 0,
                DNF = false,
                DidNotPreQualify = true   // requires adding this field to SessionResult
            }).ToList();
        }
        private GrandPrixResult CreateCompleteGrandPrixResult(List<ParticipantData> raceStandings)
        {
            var currentRace = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex);

            var result = new GrandPrixResult
            {
                GrandPrixName = currentRace.RaceName,
                Year = saveGame.CurrentSeason.Year,

                // Race results
                RaceResults = ConvertParticipantsToDriverResults(raceStandings),

                // Qualifying results (if available)
                QualifyingResults = _qualifyingResults != null
                    ? ConvertParticipantsToDriverResults(_qualifyingResults)
                    : new List<SessionResult>(),
            };

            return result;
        }

        private List<SessionResult> ConvertParticipantsToDriverResults(List<ParticipantData> participants)
        {
            return participants.Select(p => new SessionResult
            {
                DriverId = p.DriverId,
                TeamId = p.TeamId,
                Position = p.Position,
                DNF = p.DNF,
                FastestLap = p.IsSessionBestLap
            }).ToList();
        }

        private void LoadRaceWeekend()
        {
            // Set Grand Prix info
            if (saveGame.NextGpIndex < saveGame.CurrentSeason.Races.Count())
            {
                var nextRace = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex);
                _originalGrandPrixName = nextRace.RaceName;
                _originalCircuitName = nextRace.Circuit ?? "CIRCUIT";

                UpdateRaceHeaders();
            }

            // Load initial session data
            UpdateSessionDisplay(_raceDataService.CurrentSession);
        }

        private void UpdateRaceHeaders()
        {
            if (_currentStyle == GraphicsStyle.Mid2000s || _currentStyle == GraphicsStyle.Twenties2010s || _currentStyle == GraphicsStyle.Twenties2020s)
            {
                // Mid-2000s, 2010s, and 2020s: Use original case (not all caps)
                GrandPrixHeader.Text = $"{_originalGrandPrixName} {saveGame.CurrentSeason.Year}";
                CircuitHeader.Text = _originalCircuitName;
            }
            else
            {
                // Pre-94 and 1990s: Use all caps
                GrandPrixHeader.Text = $"{_originalGrandPrixName.ToUpper()} {saveGame.CurrentSeason.Year}";
                CircuitHeader.Text = _originalCircuitName.ToUpper();
            }
        }

        private void UpdateSessionDisplay(SessionData sessionData)
        {
            // Update session header with appropriate capitalization
            string sessionName = GetSessionDisplayName(sessionData.SessionType);
            SessionText.Text = sessionName;

            // Update "CURRENT SESSION: " label text
            SessionLabelText.Text = (_currentStyle == GraphicsStyle.Mid2000s || _currentStyle == GraphicsStyle.Twenties2010s || _currentStyle == GraphicsStyle.Twenties2020s)
                ? "Current Session: "
                : "CURRENT SESSION: ";

            // Check if we have valid data
            bool hasData = sessionData.Standings != null && sessionData.Standings.Any();

            if (hasData)
            {
                WaitingMessage.Visibility = Visibility.Collapsed;
                StandingsContent.Visibility = Visibility.Visible;

                // Convert ParticipantData to StandingEntry for display
                var standings = sessionData.Standings.Select(p => CreateStandingEntry(p)).ToList();

                StandingsItems.ItemsSource = standings;
            }
            else
            {
                // Show waiting message when no data is available
                WaitingMessage.Visibility = Visibility.Visible;
                StandingsContent.Visibility = Visibility.Collapsed;
            }
        }

        private StandingEntry CreateStandingEntry(ParticipantData p)
        {
            // Try to get nationality from driver data
            string nationality = "???";
            var driver = saveGame?.Drivers?.FirstOrDefault(d => d.DriverId == p.DriverId);
            if (driver != null)
            {
                // Try to extract 3-letter nationality code
                nationality = driver.Nationality?.Length >= 3 ? driver.Nationality.Substring(0, 3).ToUpper() : driver.Nationality?.ToUpper() ?? "???";
            }

            var entry = new StandingEntry
            {
                Position = p.Position,
                Number = p.Number,
                DriverId = p.DriverId,
                TeamId = p.TeamId,
                Style = _currentStyle,
                Nationality = nationality
            };

            if (_currentStyle == GraphicsStyle.Pre94)
            {
                // Pre-94 style - simple text-based layout with italics
                entry.DriverName = p.DriverName.ToUpper();
                entry.TeamName = p.TeamName.ToUpper();
                entry.BestLap = p.BestLapTime;
                entry.ShowNumber = true;
                entry.ShowNumberColumn = Visibility.Visible;
                entry.ShowPre94Layout = Visibility.Visible;
                entry.ShowModernLayout = Visibility.Collapsed;
                entry.Show2010sLayout = Visibility.Collapsed;
                entry.Show2020sLayout = Visibility.Collapsed;
                entry.RowMargin = new Thickness(0, 5, 0, 0);
                entry.PositionMargin = new Thickness(5, 0, 0, 0);
                entry.PositionAlignment = HorizontalAlignment.Left;
                entry.PositionWidth = new GridLength(30);
                entry.PositionMinWidth = 0;
                entry.PositionBorderWidth = 30;
                entry.DriverNameTextMargin = new Thickness(5, 0, 0, 0);
                entry.TeamNameTextMargin = new Thickness(5, 0, 0, 0);
                entry.BestLapTextMargin = new Thickness(5, 0, 0, 0);
                entry.NatColumnWidth = new GridLength(50); // 50px for NAT column in Pre-94
                entry.NumberColumnWidth = new GridLength(50);
                entry.FontStyleValue = FontStyles.Italic;

                // All transparent backgrounds for Pre-94 style
                entry.PositionBackground = Brushes.Transparent;
                entry.PositionForeground = Brushes.White;
                entry.DriverNameBackground = Brushes.Transparent;
                entry.DriverNameForeground = Brushes.White;
                entry.TeamNameBackground = Brushes.Transparent;
                entry.TeamNameForeground = new SolidColorBrush(Color.FromRgb(255, 255, 0)); // Yellow for nationality
                entry.BestLapBackground = Brushes.Transparent;
                entry.BestLapForeground = Brushes.White;
                entry.LapTimeColor = Brushes.White;
            }
            else if (_currentStyle == GraphicsStyle.Nineties1990s)
            {
                // 1990s style - yellow position boxes, light blue number ovals, white text
                entry.DriverName = p.DriverName.ToUpper();
                entry.TeamName = p.TeamName.ToUpper();
                entry.BestLap = p.BestLapTime;
                entry.ShowPre94Layout = Visibility.Collapsed;
                entry.ShowModernLayout = Visibility.Visible;
                entry.Show2010sLayout = Visibility.Collapsed;
                entry.Show2020sLayout = Visibility.Collapsed;
                entry.RowMargin = new Thickness(0, 5, 0, 0);
                entry.PositionMargin = new Thickness(5, 0, 0, 0);
                entry.PositionAlignment = HorizontalAlignment.Left;
                entry.PositionWidth = new GridLength(30);
                entry.PositionMinWidth = 0; // No min width for proper centering
                entry.PositionBorderWidth = 30;
                entry.DriverNameTextMargin = new Thickness(5, 0, 0, 0);
                entry.TeamNameTextMargin = new Thickness(5, 0, 0, 0);
                entry.BestLapTextMargin = new Thickness(5, 0, 0, 0);
                entry.NatColumnWidth = new GridLength(0); // Hide NAT column in 1990s
                entry.NumberColumnWidth = new GridLength(50);
                entry.FontStyleValue = FontStyles.Normal;
                entry.ShowNumberColumn = Visibility.Visible;

                entry.PositionBackground = new SolidColorBrush(Color.FromRgb(255, 255, 0)); // Yellow
                entry.PositionForeground = Brushes.Black;
                entry.ShowNumber = true;
                entry.DriverNameBackground = Brushes.Transparent;
                entry.DriverNameForeground = Brushes.White;
                entry.TeamNameBackground = Brushes.Transparent;
                entry.TeamNameForeground = Brushes.White;
                entry.BestLapBackground = Brushes.Transparent;
                entry.BestLapForeground = p.IsSessionBestLap
                    ? new SolidColorBrush(Color.FromRgb(255, 255, 0)) // Yellow for fastest
                    : Brushes.White;
                entry.LapTimeColor = entry.BestLapForeground;
            }
            else if (_currentStyle == GraphicsStyle.Mid2000s)
            {
                // Keep original case for Mid-2000s
                entry.DriverName = p.DriverName;
                entry.TeamName = p.TeamName;
                entry.BestLap = p.BestLapTime;
                entry.ShowPre94Layout = Visibility.Collapsed;
                entry.ShowModernLayout = Visibility.Visible;
                entry.Show2010sLayout = Visibility.Collapsed;
                entry.Show2020sLayout = Visibility.Collapsed;
                entry.RowMargin = new Thickness(0); // No margin for Mid-2000s
                entry.PositionMargin = new Thickness(0); // No margin on position box
                entry.PositionAlignment = HorizontalAlignment.Stretch; // Fill entire column
                entry.PositionWidth = new GridLength(0); // Not used - Border will stretch
                entry.PositionMinWidth = 0; // No min width restriction
                entry.PositionBorderWidth = double.NaN; // NaN = stretch to fill
                entry.DriverNameTextMargin = new Thickness(5, 0, 0, 0); // Only left padding
                entry.TeamNameTextMargin = new Thickness(5, 0, 0, 0); // Add left padding
                entry.BestLapTextMargin = new Thickness(5, 0, 0, 0); // Add left padding
                entry.NatColumnWidth = new GridLength(0); // Hide NAT column in Mid-2000s
                entry.NumberColumnWidth = new GridLength(0);
                entry.FontStyleValue = FontStyles.Normal;
                entry.ShowNumberColumn = Visibility.Collapsed; // Hide number column in mid-2000s

                // Position box - dark gray for most, red for P1
                if (p.Position == 1)
                {
                    entry.PositionBackground = new LinearGradientBrush(
                        Color.FromRgb(200, 0, 0),    // Dark red
                        Color.FromRgb(100, 0, 0),    // Darker red
                        90);
                }
                else
                {
                    entry.PositionBackground = new LinearGradientBrush(
                        Color.FromRgb(80, 80, 80),   // Dark gray
                        Color.FromRgb(40, 40, 40),   // Darker gray
                        90);
                }
                entry.PositionForeground = Brushes.White;
                entry.ShowNumber = false; // Hide number in mid-2000s style

                // Driver name - silver gradient background, black text
                entry.DriverNameBackground = new LinearGradientBrush(
                    Color.FromRgb(200, 200, 200),  // Light silver
                    Color.FromRgb(140, 140, 140),  // Darker silver
                    90);
                entry.DriverNameForeground = Brushes.Black;

                // Team name - dark gray gradient background, white text
                entry.TeamNameBackground = new LinearGradientBrush(
                    Color.FromRgb(80, 80, 80),   // Dark gray
                    Color.FromRgb(40, 40, 40),   // Darker gray
                    90);
                entry.TeamNameForeground = Brushes.White;

                // Best lap - black gradient background, white text
                entry.BestLapBackground = new LinearGradientBrush(
                    Color.FromRgb(50, 50, 50),   // Dark gray/black
                    Color.FromRgb(0, 0, 0),      // Black
                    90);
                entry.BestLapForeground = Brushes.White;
                entry.LapTimeColor = entry.BestLapForeground;
            }
            else if (_currentStyle == GraphicsStyle.Twenties2010s)
            {
                // 2010s style - glossy backgrounds, skewed look
                entry.DriverName = p.DriverName.ToUpper(); // Driver names in CAPS
                entry.TeamName = p.TeamName; // Team names normal case
                entry.BestLap = p.BestLapTime;
                entry.ShowPre94Layout = Visibility.Collapsed;
                entry.ShowModernLayout = Visibility.Collapsed;
                entry.Show2010sLayout = Visibility.Visible;
                entry.Show2020sLayout = Visibility.Collapsed;
                entry.RowMargin = new Thickness(0); // No margin
                entry.PositionMargin = new Thickness(0);
                entry.PositionAlignment = HorizontalAlignment.Stretch;
                entry.PositionWidth = new GridLength(1, GridUnitType.Star);
                entry.PositionMinWidth = 0;
                entry.PositionBorderWidth = double.NaN; // Stretch to fill
                entry.DriverNameTextMargin = new Thickness(8, 0, 5, 0);
                entry.TeamNameTextMargin = new Thickness(8, 0, 5, 0);
                entry.BestLapTextMargin = new Thickness(5, 0, 8, 0);
                entry.NatColumnWidth = new GridLength(0); // Hide NAT column
                entry.NumberColumnWidth = new GridLength(0);
                entry.FontStyleValue = FontStyles.Normal;
                entry.ShowNumberColumn = Visibility.Collapsed;

                // Position - Red for P1, black for others
                if (p.Position == 1)
                {
                    entry.PositionBackground = new LinearGradientBrush(
                        Color.FromRgb(200, 0, 0),    // Red
                        Color.FromRgb(120, 0, 0),    // Darker red
                        new Point(0, 0), new Point(1, 1));
                }
                else
                {
                    entry.PositionBackground = new LinearGradientBrush(
                        Color.FromRgb(40, 40, 40),   // Dark
                        Color.FromRgb(10, 10, 10),   // Darker
                        new Point(0, 0), new Point(1, 1));
                }
                entry.PositionForeground = Brushes.White;

                // Driver name - glossy black background, white text
                entry.DriverNameBackground = new LinearGradientBrush(
                    Color.FromRgb(50, 50, 50),   // Light black
                    Color.FromRgb(10, 10, 10),   // Dark black
                    new Point(0, 0), new Point(1, 1));
                entry.DriverNameForeground = Brushes.White;

                // Team name - dark gray glossy background, white text
                entry.TeamNameBackground = new LinearGradientBrush(
                    Color.FromRgb(80, 80, 80),   // Light gray
                    Color.FromRgb(40, 40, 40),   // Darker gray
                    new Point(0, 0), new Point(1, 1));
                entry.TeamNameForeground = Brushes.White;

                // Best lap - glossy black background, white text
                entry.BestLapBackground = new LinearGradientBrush(
                    Color.FromRgb(50, 50, 50),   // Light black
                    Color.FromRgb(10, 10, 10),   // Dark black
                    new Point(0, 0), new Point(1, 1));
                entry.BestLapForeground = Brushes.White;
                entry.LapTimeColor = entry.BestLapForeground;
            }
            else if (_currentStyle == GraphicsStyle.Twenties2020s)
            {
                // 2020s style - clean modern look with team color accent
                entry.DriverName = p.DriverName; // Normal capitalization, bold
                entry.TeamName = p.TeamName; // Normal capitalization, not bold
                entry.BestLap = p.BestLapTime;
                entry.ShowPre94Layout = Visibility.Collapsed;
                entry.ShowModernLayout = Visibility.Collapsed;
                entry.Show2010sLayout = Visibility.Collapsed;
                entry.Show2020sLayout = Visibility.Visible;
                entry.RowMargin = new Thickness(0, 2, 0, 0); // Small gap between rows
                entry.PositionMargin = new Thickness(0, 0, 15, 0);
                entry.PositionAlignment = HorizontalAlignment.Left; // Left-aligned, not stretched
                entry.PositionBorderWidth = 30; // Fixed 45px width
                entry.PositionMinWidth = 0;
                entry.DriverNameTextMargin = new Thickness(15, 0, 5, 0);
                entry.TeamNameTextMargin = new Thickness(5, 0, 5, 0);
                entry.BestLapTextMargin = new Thickness(5, 0, 5, 0);
                entry.NatColumnWidth = new GridLength(0);
                entry.NumberColumnWidth = new GridLength(0);
                entry.FontStyleValue = FontStyles.Normal;
                entry.ShowNumberColumn = Visibility.Collapsed;

                // Position - white box with black number
                entry.PositionBackground = Brushes.White;
                entry.PositionForeground = Brushes.Black;

                // Driver name - dark background, white text
                entry.DriverNameBackground = Brushes.Black;
                entry.DriverNameForeground = Brushes.White;

                // Team name - darker background, white text
                entry.TeamNameBackground = Brushes.Black;
                entry.TeamNameForeground = Brushes.White;

                // Best lap - black background, white text
                entry.BestLapBackground = Brushes.Black;
                entry.BestLapForeground = Brushes.White;
                entry.LapTimeColor = entry.BestLapForeground;
            }

            // Team color accent (left border of driver name)
            // Get team color from team data
            var team = saveGame?.CurrentSeason?.Teams?.FirstOrDefault(t => t.TeamId == p.TeamId);
            if (team != null && !string.IsNullOrEmpty(team.Color))
            {
                try
                {
                    entry.TeamColorAccent = (SolidColorBrush)new BrushConverter().ConvertFromString(team.Color);
                }
                catch
                {
                    entry.TeamColorAccent = new SolidColorBrush(Color.FromRgb(0, 255, 255)); // Cyan fallback
                }
            }
            else
            {
                entry.TeamColorAccent = new SolidColorBrush(Color.FromRgb(0, 255, 255)); // Cyan fallback
            }

            return entry;
        }

        private string GetSessionDisplayName(SessionType sessionType)
        {
            if (_currentStyle == GraphicsStyle.Mid2000s || _currentStyle == GraphicsStyle.Twenties2010s || _currentStyle == GraphicsStyle.Twenties2020s)
            {
                return sessionType switch
                {
                    SessionType.Practice => "Practice",
                    SessionType.Qualification => "Qualification",
                    SessionType.Race => "Race",
                    _ => "Unknown Session"
                };
            }
            else
            {
                return sessionType switch
                {
                    SessionType.Practice => "PRACTICE",
                    SessionType.Qualification => "QUALIFICATION",
                    SessionType.Race => "RACE",
                    _ => "UNKNOWN SESSION"
                };
            }
        }

        private void NextSession_Click(object sender, RoutedEventArgs e)
        {
            var currentSession = _raceDataService.CurrentSession;

            if (currentSession.IsSessionActive)
            {
                MessageBox.Show("Please finish the current session first!", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // For user controlled mock service, the control window handles session progression
            if (_raceDataService is MockUserControlledRaceDataService)
            {
                MessageBox.Show("Use the Race Control window to advance sessions.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events
            _raceDataService.SessionUpdated -= OnSessionUpdated;
            _raceDataService.SessionFinished -= OnSessionFinished;

            if (_preQualiMode)
                _raceDataService.IsPreQualiSession = false;

            if (_raceDataService.IsRunning)
            {
                _raceDataService.Stop();
            }

            if (_raceDataService.IsRunning)
                _raceDataService.Stop();

            // Close control window if it exists
            _controlWindow?.Close();

            base.OnClosed(e);
        }

        private void UpdateHeadersForStyle()
        {
            if (this.FindName("ColumnHeaders") is Grid headers)
            {
                // Hide headers for Mid-2000s, 2010s, and 2020s; show for Pre-94 and 1990s
                headers.Visibility = (_currentStyle == GraphicsStyle.Mid2000s || _currentStyle == GraphicsStyle.Twenties2010s || _currentStyle == GraphicsStyle.Twenties2020s)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Set NAT column width in header grid
            if (this.FindName("NatColumnDefinition") is ColumnDefinition natCol)
            {
                natCol.Width = _currentStyle == GraphicsStyle.Pre94
                    ? new GridLength(50)
                    : new GridLength(0);
            }

            // Hide/show NAT column header based on style
            if (this.FindName("NationalityColumnHeader") is TextBlock natHeader)
            {
                // NAT column only visible in Pre-94 style
                natHeader.Visibility = _currentStyle == GraphicsStyle.Pre94
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // Control number column header visibility based on style
            if (this.FindName("NumberColumnHeader") is TextBlock numberHeader)
            {
                numberHeader.Visibility = (_currentStyle == GraphicsStyle.Mid2000s || _currentStyle == GraphicsStyle.Twenties2010s || _currentStyle == GraphicsStyle.Twenties2020s)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void UpdateUIColorsForStyle()
        {
            if (MainBackground == null ||
                SessionBanner == null ||
                SessionLabelText == null ||
                SessionText == null ||
                GrandPrixHeader == null ||
                CircuitHeader == null ||
                EndRaceButton == null)
                return;

            if (_currentStyle == GraphicsStyle.Twenties2020s)
            {
                // 2020s style: Black theme

                // Main background - black
                MainBackground.Background = Brushes.Black;

                // Session banner - black
                SessionBanner.Background = Brushes.Black;

                // Session text - white text
                SessionLabelText.Foreground = Brushes.White;
                SessionLabelText.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Soniano Sans Unicode");
                SessionText.Foreground = Brushes.White;
                SessionText.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Soniano Sans Unicode");

                // Race/Circuit headers - white text
                GrandPrixHeader.Foreground = Brushes.White;
                GrandPrixHeader.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Soniano Sans Unicode");
                CircuitHeader.Foreground = Brushes.White;
                CircuitHeader.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Soniano Sans Unicode");

                // End Race button - black background
                EndRaceButton.Background = Brushes.Black;
                EndRaceButton.Foreground = Brushes.White;
                EndRaceButton.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Soniano Sans Unicode");
            }
            else if (_currentStyle == GraphicsStyle.Mid2000s || _currentStyle == GraphicsStyle.Twenties2010s)
            {
                // Mid-2000s and 2010s style: Light gray background, dark gray gradients

                // Main background - light gray
                MainBackground.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));

                // Session banner - dark gray gradient
                SessionBanner.Background = new LinearGradientBrush(
                    Color.FromRgb(80, 80, 80),
                    Color.FromRgb(40, 40, 40),
                    90);

                // Session text - white text
                SessionLabelText.Foreground = Brushes.White;
                SessionLabelText.FontFamily = new FontFamily("Calibri");
                SessionText.Foreground = Brushes.White;
                SessionText.FontFamily = new FontFamily("Calibri");

                // Race/Circuit headers - black text
                GrandPrixHeader.Foreground = Brushes.Black;
                GrandPrixHeader.FontFamily = new FontFamily("Calibri");
                CircuitHeader.Foreground = Brushes.Black;
                CircuitHeader.FontFamily = new FontFamily("Calibri");

                // End Race button - dark gray gradient
                EndRaceButton.Background = new LinearGradientBrush(
                    Color.FromRgb(80, 80, 80),
                    Color.FromRgb(40, 40, 40),
                    90);
                EndRaceButton.Foreground = Brushes.White;
                EndRaceButton.FontFamily = new FontFamily("Calibri");
            }
            else
            {
                // Pre-94 and 1990s style: Dark gray background, yellow boxes

                // Main background - dark semi-transparent gray
                MainBackground.Background = new SolidColorBrush(Color.FromArgb(204, 60, 60, 60)); // #CC3C3C3C

                // Session banner - yellow
                SessionBanner.Background = new SolidColorBrush(Color.FromRgb(255, 255, 0));

                // Session text - black text
                SessionLabelText.Foreground = Brushes.Black;
                SessionLabelText.FontFamily = new FontFamily("Calibri");
                SessionText.Foreground = Brushes.Black;
                SessionText.FontFamily = new FontFamily("Calibri");

                // Race/Circuit headers - white text
                GrandPrixHeader.Foreground = Brushes.White;
                GrandPrixHeader.FontFamily = new FontFamily("Calibri");
                CircuitHeader.Foreground = Brushes.White;
                CircuitHeader.FontFamily = new FontFamily("Calibri");

                // End Race button - yellow
                EndRaceButton.Background = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                EndRaceButton.Foreground = Brushes.Black;
                EndRaceButton.FontFamily = new FontFamily("Calibri");
            }

            // Update race headers (handles capitalization)
            UpdateRaceHeaders();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Unsubscribe to ensure this only runs once
            this.ContentRendered -= Window_ContentRendered;

            // Run simulation
            var simService = _raceDataService as SimulatedRaceDataService;
            simService?.SimulateRaceWeekend();
        }
    }
}