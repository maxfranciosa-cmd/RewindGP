using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Windows;
using System.Windows.Controls;
using RadioButton = System.Windows.Controls.RadioButton;

namespace AMS2ChEd.Views
{
    public enum PaddockOpportunitiesContext
    {
        // a pay driver starting a career with no team
        GameStart,
        // a race goes on without the player, the next one is coming
        NextRace,
        // the new season starts without a race seat for the player
        NewSeason
    }

    /// <summary>
    /// What the paddock offers a player without a race seat at one race (PaddockOpportunities):
    /// the absences they can apply for on race day, the races they already stand in for, and the
    /// rides a pay driver can buy. Buying one adds its PAID_RIDE absence to the save.
    /// </summary>
    public partial class PaddockOpportunitiesWindow : Window
    {
        private readonly ISaveGame _saveGame;
        private readonly PaddockOpportunities _opportunities;
        private PaidRideOffer _selectedOffer;

        public bool RideBought { get; private set; }

        /// <summary>
        /// Shows the window for this race and returns false only when the player pressed Back (offered
        /// at game start). With onlyWhenThereAreChoices nothing is shown unless the player has
        /// something to decide.
        /// </summary>
        public static bool ShowFor(Window owner, ISaveGame saveGame, int raceIndex, PaddockOpportunitiesContext context, bool onlyWhenThereAreChoices = false)
        {
            var opportunities = PaddockOpportunities.For(saveGame, raceIndex);
            if (opportunities.Race == null || (onlyWhenThereAreChoices && !opportunities.HasChoices))
                return true;

            var window = new PaddockOpportunitiesWindow(saveGame, opportunities, context);
            if (owner != null && owner.IsLoaded)
                window.Owner = owner;

            return window.ShowDialog() == true;
        }

        private PaddockOpportunitiesWindow(ISaveGame saveGame, PaddockOpportunities opportunities, PaddockOpportunitiesContext context)
        {
            InitializeComponent();
            _saveGame = saveGame;
            _opportunities = opportunities;

            var raceName = opportunities.Race.RaceName;
            TitleText.Text = string.Format(Strings.PaddockOpportunitiesWindow_Title_Format, raceName);
            IntroText.Text = string.Format(context switch
            {
                PaddockOpportunitiesContext.GameStart => Strings.PaddockOpportunitiesWindow_Intro_GameStart_Format,
                PaddockOpportunitiesContext.NewSeason => Strings.PaddockOpportunitiesWindow_Intro_NewSeason_Format,
                _ => Strings.PaddockOpportunitiesWindow_Intro_NextRace_Format
            }, raceName);

            BackButton.Visibility = context == PaddockOpportunitiesContext.GameStart ? Visibility.Visible : Visibility.Collapsed;

            BuildContent();
        }

        private void BuildContent()
        {
            var nothingOnOffer = true;

            if (_opportunities.ReserveTeamId != null)
            {
                AddParagraph(string.Format(Strings.PaddockOpportunitiesWindow_ReserveNote_Format, TeamName(_opportunities.ReserveTeamId)), muted: true);
            }

            if (_opportunities.CallUps.Any())
            {
                nothingOnOffer = false;
                AddHeading(Strings.PaddockOpportunitiesWindow_CallUpsHeading);
                foreach (var callUp in _opportunities.CallUps)
                {
                    var format = callUp.Reason == AbsenceReason.PAID_RIDE
                        ? Strings.PaddockOpportunitiesWindow_PaidCallUp_Format
                        : Strings.PaddockOpportunitiesWindow_CallUp_Format;
                    AddParagraph(string.Format(format, DriverName(callUp.DriverOut), TeamName(callUp.TeamId)));
                }
            }

            if (_opportunities.OpenAbsences.Any())
            {
                nothingOnOffer = false;
                AddHeading(Strings.PaddockOpportunitiesWindow_AbsencesHeading);
                foreach (var absence in _opportunities.OpenAbsences)
                {
                    AddParagraph(string.Format(Strings.PaddockOpportunitiesWindow_Absence_Format,
                        DriverName(absence.DriverOut), TeamName(absence.TeamId), DriverName(absence.DriverIn)));
                }
                AddParagraph(Strings.PaddockOpportunitiesWindow_AbsencesNote, muted: true);
            }

            if (_opportunities.PaidRidesBudget > 0)
            {
                nothingOnOffer = false;
                AddHeading(Strings.PaddockOpportunitiesWindow_PaidRidesHeading);
                AddParagraph(string.Format(Strings.PaddockOpportunitiesWindow_Budget_Format, _opportunities.PaidRidesRemaining, _opportunities.PaidRidesBudget));

                if (_opportunities.PaidRidesRemaining == 0)
                {
                    AddParagraph(Strings.PaddockOpportunitiesWindow_BudgetSpent, muted: true);
                }
                else if (_opportunities.CallUps.Any())
                {
                    AddParagraph(Strings.PaddockOpportunitiesWindow_AlreadyRacing, muted: true);
                }
                else if (!_opportunities.PaidRideOffers.Any())
                {
                    AddParagraph(Strings.PaddockOpportunitiesWindow_NoSeatForSale, muted: true);
                }
                else
                {
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
            }

            if (nothingOnOffer)
            {
                AddParagraph(Strings.PaddockOpportunitiesWindow_NothingOnOffer, muted: true);
            }

            BuyButton.Visibility = _opportunities.PaidRideOffers.Any() && !_opportunities.CallUps.Any() ? Visibility.Visible : Visibility.Collapsed;
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
            if (string.IsNullOrEmpty(driverId))
                return Strings.SeasonOverviewWindow_UnknownDriver;
            if (driverId == _saveGame.PlayerData.DriverId)
                return _saveGame.PlayerData.Name;
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
