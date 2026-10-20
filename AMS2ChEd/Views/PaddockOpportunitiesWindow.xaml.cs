using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Resources;
using System.Windows;
using System.Windows.Controls;
using RadioButton = System.Windows.Controls.RadioButton;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The rides a pay driver without a car can buy at one race (PaddockOpportunities), offered
    /// when the race's entry list is drawn up, once its absences are settled. Buying one adds its
    /// PAID_RIDE absence to the save.
    /// </summary>
    public partial class PaddockOpportunitiesWindow : Window
    {
        private readonly ISaveGame _saveGame;
        private readonly PaddockOpportunities _opportunities;
        private PaidRideOffer _selectedOffer;

        public bool RideBought { get; private set; }

        // shows the window only when a ride is on sale at this race
        public static void ShowFor(Window owner, ISaveGame saveGame, int raceIndex)
        {
            var opportunities = PaddockOpportunities.For(saveGame, raceIndex);
            if (opportunities.Race == null || !opportunities.HasChoices)
                return;

            var window = new PaddockOpportunitiesWindow(saveGame, opportunities);
            if (owner != null && owner.IsLoaded)
                window.Owner = owner;

            window.ShowDialog();
        }

        private PaddockOpportunitiesWindow(ISaveGame saveGame, PaddockOpportunities opportunities)
        {
            InitializeComponent();
            _saveGame = saveGame;
            _opportunities = opportunities;

            var raceName = opportunities.Race.RaceName;
            TitleText.Text = string.Format(Strings.PaddockOpportunitiesWindow_Title_Format, raceName);
            IntroText.Text = string.Format(Strings.PaddockOpportunitiesWindow_Intro_NextRace_Format, raceName);

            BuildContent();
        }

        private void BuildContent()
        {
            AddHeading(Strings.PaddockOpportunitiesWindow_PaidRidesHeading);
            AddParagraph(string.Format(Strings.PaddockOpportunitiesWindow_Budget_Format, _opportunities.PaidRidesRemaining, _opportunities.PaidRidesBudget));

            foreach (var offer in _opportunities.PaidRideOffers)
            {
                var radioButton = new RadioButton
                {
                    Content = new TextBlock
                    {
                        Text = string.Format(Strings.PaddockOpportunitiesWindow_Offer_Format, TeamName(offer.TeamId), DriverName(offer.DriverOutId)),
                        TextWrapping = TextWrapping.Wrap
                    },
                    GroupName = "PaidRides",
                    Tag = offer,
                    Margin = new Thickness(0, 4, 0, 4),
                    Style = (Style)FindResource("Charcoal.RadioButtonStyle")
                };
                radioButton.Checked += (_, _) =>
                {
                    _selectedOffer = offer;
                    BuyButton.IsEnabled = true;
                };
                ContentPanel.Children.Add(radioButton);
            }

            AddParagraph(Strings.PaddockOpportunitiesWindow_PaidRidesNote, muted: true);
        }

        private void AddHeading(string text)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = text,
                Style = (Style)FindResource("Charcoal.HeadingTextStyle"),
                Margin = new Thickness(0, 14, 0, 6)
            });
        }

        private void AddParagraph(string text, bool muted = false)
        {
            var paragraph = new TextBlock
            {
                Text = text,
                Style = (Style)FindResource("Charcoal.BodyTextStyle"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 4)
            };
            paragraph.Foreground = (System.Windows.Media.Brush)FindResource(muted ? "Charcoal.TextMutedBrush" : "Charcoal.TextSoftBrush");
            if (muted)
                paragraph.FontStyle = FontStyles.Italic;
            ContentPanel.Children.Add(paragraph);
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

            PaidRides.Buy(_saveGame, _opportunities.RaceIndex, _selectedOffer);
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
