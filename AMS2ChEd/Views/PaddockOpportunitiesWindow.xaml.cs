using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Resources;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ColorConverter = System.Windows.Media.ColorConverter;
using Image = System.Windows.Controls.Image;
using RadioButton = System.Windows.Controls.RadioButton;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The rides a pay driver without a car can buy at one race (PaddockOpportunities), offered
    /// when the race's entry list is drawn up, once its absences are settled and the AI pay drivers
    /// have bought theirs. Buying one adds its PAID_RIDE absence to the save, or takes over the AI
    /// driver's ride when the team picks the player over them. Each seat on sale is a tile
    /// picturing the team's car (ITeamPreviewProvider), or its colour when the game has no picture.
    /// </summary>
    public partial class PaddockOpportunitiesWindow : Window
    {
        private const double PictureHeight = 130;

        // twice a tile's width, so the picture stays sharp on a high-DPI screen
        private const int PictureMaxWidth = 640;

        private readonly ISaveGame _saveGame;
        private readonly PaddockOpportunities _opportunities;
        private readonly ITeamPreviewProvider _previewProvider;
        private PaidRideOffer _selectedOffer;

        // seats the team gave to the AI driver who bid first
        private readonly HashSet<PaidRideOffer> _lostOffers = new();

        // each team's picture, loaded once off the UI thread: the tiles are rebuilt after a lost bid
        private readonly Dictionary<string, Task<ImageSource>> _pictures = new();

        public bool RideBought { get; private set; }

        // shows the window only when a ride is on sale at this race
        public static void ShowFor(Window owner, ISaveGame saveGame, int raceIndex, ITeamPreviewProvider previewProvider = null)
        {
            var opportunities = PaddockOpportunities.For(saveGame, raceIndex);
            if (opportunities.Race == null || !opportunities.HasChoices)
                return;

            var window = new PaddockOpportunitiesWindow(saveGame, opportunities, previewProvider);
            if (owner != null && owner.IsLoaded)
                window.Owner = owner;

            window.ShowDialog();
        }

        private PaddockOpportunitiesWindow(ISaveGame saveGame, PaddockOpportunities opportunities, ITeamPreviewProvider previewProvider)
        {
            InitializeComponent();
            _saveGame = saveGame;
            _opportunities = opportunities;
            _previewProvider = previewProvider;

            var raceName = opportunities.Race.RaceName;
            TitleText.Text = string.Format(Strings.PaddockOpportunitiesWindow_Title_Format, raceName);
            IntroText.Text = string.Format(Strings.PaddockOpportunitiesWindow_Intro_NextRace_Format, raceName);

            RidesLeftRun.Text = opportunities.PaidRidesRemaining.ToString(CultureInfo.CurrentCulture);
            RidesTotalRun.Text = "/" + opportunities.PaidRidesBudget.ToString(CultureInfo.CurrentCulture);
            BudgetBadge.ToolTip = string.Format(Strings.PaddockOpportunitiesWindow_Budget_Format, opportunities.PaidRidesRemaining, opportunities.PaidRidesBudget);

            BuildContent();
        }

        private void BuildContent(string bidLostText = null)
        {
            TilesPanel.Children.Clear();
            SelectOffer(null);

            BidLostText.Text = bidLostText ?? string.Empty;
            BidLostText.Visibility = bidLostText == null ? Visibility.Collapsed : Visibility.Visible;

            foreach (var offer in _opportunities.PaidRideOffers.Where(o => !_lostOffers.Contains(o)))
            {
                var tile = new RadioButton
                {
                    Content = BuildTile(offer),
                    Tag = offer,
                    Style = (Style)FindResource("PaddockOpportunities.TileStyle")
                };
                System.Windows.Automation.AutomationProperties.SetName(tile, string.Format(Strings.PaddockOpportunitiesWindow_Tile_Replaces_Format, DriverName(offer.DriverOutId)) + ", " + TeamName(offer.TeamId));
                tile.Checked += (_, _) => SelectOffer(offer);
                TilesPanel.Children.Add(tile);
            }
        }

        private UIElement BuildTile(PaidRideOffer offer)
        {
            var panel = new StackPanel();

            // the car, fading in over the team's colour once loaded (all that shows without a picture)
            var picture = new Grid
            {
                Height = PictureHeight,
                ClipToBounds = true,
                Background = TeamBrush(offer.TeamId)
            };
            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            };
            picture.Children.Add(image);
            ShowPicture(image, offer.TeamId);
            if (offer.RivalBidderId != null)
            {
                picture.Children.Add(new Border
                {
                    Background = (Brush)FindResource("Charcoal.RedBrush"),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Child = new TextBlock
                    {
                        Text = Strings.PaddockOpportunitiesWindow_Tile_ContestedBadge,
                        Style = (Style)FindResource("Charcoal.HeadingTextStyle"),
                        FontSize = 13
                    }
                });
            }
            panel.Children.Add(picture);

            var caption = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };
            caption.Children.Add(new TextBlock
            {
                Text = TeamName(offer.TeamId).ToUpper(CultureInfo.CurrentCulture),
                Style = (Style)FindResource("Charcoal.HeadingTextStyle"),
                FontSize = 21,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            caption.Children.Add(new TextBlock
            {
                Text = string.Format(Strings.PaddockOpportunitiesWindow_Tile_Replaces_Format, DriverName(offer.DriverOutId)),
                Style = (Style)FindResource("Charcoal.BodyTextStyle"),
                FontSize = 14,
                Foreground = (Brush)FindResource("Charcoal.TextSoftBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            caption.Children.Add(new TextBlock
            {
                Text = offer.RivalBidderId == null
                    ? Strings.PaddockOpportunitiesWindow_Tile_Open
                    : string.Format(Strings.PaddockOpportunitiesWindow_Tile_Contested_Format, DriverName(offer.RivalBidderId)),
                Style = (Style)FindResource("Charcoal.BodyTextStyle"),
                FontSize = 12,
                Foreground = (Brush)FindResource(offer.RivalBidderId == null ? "Charcoal.TextMutedBrush" : "Charcoal.AccentTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
            panel.Children.Add(caption);

            return panel;
        }

        private void SelectOffer(PaidRideOffer offer)
        {
            _selectedOffer = offer;
            BuyButton.IsEnabled = offer != null;
            BuyButton.Content = offer == null
                ? Strings.PaddockOpportunitiesWindow_BuyButton
                : string.Format(Strings.PaddockOpportunitiesWindow_BuyTeamSeat_Format, TeamName(offer.TeamId).ToUpper(CultureInfo.CurrentCulture));
        }

        private async void ShowPicture(Image image, string teamId)
        {
            if (_previewProvider == null)
                return;

            if (!_pictures.TryGetValue(teamId, out var picture))
            {
                var season = _saveGame.CurrentSeason;
                var raceId = _opportunities.Race.RaceId;
                picture = Task.Run(() => LoadPicture(season, teamId, raceId));
                _pictures[teamId] = picture;
            }

            ImageSource source;
            try
            {
                source = await picture;
            }
            catch (Exception ex)
            {
                // a picture that can't be loaded only costs the tile its picture
                Console.WriteLine($"Warning: team picture could not be loaded for {teamId} ({ex.Message})");
                return;
            }

            if (source == null)
                return;

            image.Source = source;
            image.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
        }

        // runs off the UI thread: the bitmap is frozen so the UI thread can use it
        private ImageSource LoadPicture(ISeason season, string teamId, int raceId)
        {
            var png = _previewProvider.LoadTeamPreviewPng(season, teamId, raceId, PictureMaxWidth);
            if (png == null)
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(png);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private Brush TeamBrush(string teamId)
        {
            var color = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId)?.Color;
            if (!string.IsNullOrEmpty(color))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                }
                catch (FormatException)
                {
                }
            }
            return (Brush)FindResource("Charcoal.StripBrush");
        }

        private string DriverName(string driverId)
        {
            return _saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId)?.Name ?? Strings.SeasonOverviewWindow_UnknownDriver;
        }

        private string TeamName(string teamId)
        {
            return _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId)?.TeamName ?? Strings.SeasonOverviewWindow_UnknownTeam;
        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOffer == null)
                return;

            // the seat an AI driver bought first can go to them: then the player picks again
            if (PaidRides.Buy(_saveGame, _opportunities.RaceIndex, _selectedOffer) == null)
            {
                var lost = _selectedOffer;
                _lostOffers.Add(lost);
                BuildContent(string.Format(Strings.PaddockOpportunitiesWindow_BidLost_Format, TeamName(lost.TeamId), DriverName(lost.RivalBidderId)));
                return;
            }

            RideBought = true;
            DialogResult = true;
            Close();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
