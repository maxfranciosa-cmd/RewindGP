using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace AMS2ChEd.Views
{
    public class AcademyCardRow
    {
        public string Name { get; set; }
        public string Teams { get; set; }
        public bool IsPlayersAcademy { get; set; }
        public Visibility YourAcademyVisibility { get; set; }
        public List<AcademyMemberRow> Members { get; set; }
        public Visibility NoMembersVisibility { get; set; }
        public string Former { get; set; }
        public Visibility FormerVisibility { get; set; }
    }

    public class AcademyMemberRow
    {
        public int Rank { get; set; }
        public string Name { get; set; }
        // age, and whether this is his last season in the academy
        public string Detail { get; set; }
        public bool IsPlayer { get; set; }
        public bool AgesOut { get; set; }
        public Brush TeamBrush { get; set; }
        public string SeatTeam { get; set; }
        public string SeatDetail { get; set; }
        public string Reputation { get; set; }
        public string Practice { get; set; }
        // what his FP1 average is worth at the end of the season (only for a member without a race seat)
        public string PracticeOutlook { get; set; }
        public Visibility PracticeOutlookVisibility { get; set; }
        public string Season { get; set; }
    }

    /// <summary>
    /// The current season's junior academies (AcademyStandings): every member, where he drives, his
    /// FP1 outings and his season, in each academy's pecking order; the player's academy first.
    /// </summary>
    public partial class AcademiesWindow : Window
    {
        public AcademiesWindow(ISaveGame saveGame)
        {
            InitializeComponent();

            KickerText.Text = string.Format(Strings.AcademiesWindow_Kicker_Format, saveGame.CurrentSeason.Year);

            var noTeamBrush = (Brush)FindResource("Academies.NoTeamBrush");
            var teams = saveGame.CurrentSeason.Teams ?? Enumerable.Empty<ITeamEntry>();
            var cards = AcademyStandings.Build(saveGame).Select(entry => ToCard(entry, teams, noTeamBrush)).ToList();

            AcademiesItems.ItemsSource = cards;
            NoAcademiesText.Visibility = cards.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private static AcademyCardRow ToCard(AcademyStandingsEntry entry, IEnumerable<ITeamEntry> teams, Brush noTeamBrush)
        {
            var name = AcademyLetterWindow.AcademyName(entry.Academy, teams);
            var formerNames = entry.FormerMembers
                .Select(m => string.IsNullOrEmpty(m.TeamShortName) ? m.Name : string.Format(Strings.AcademiesWindow_FormerEntry_Format, m.Name, m.TeamShortName))
                .ToList();

            return new AcademyCardRow
            {
                Name = string.IsNullOrEmpty(name) ? name : char.ToUpper(name[0]) + name.Substring(1),
                Teams = AcademyLetterWindow.AcademyTeams(entry.Academy, teams),
                IsPlayersAcademy = entry.IsPlayersAcademy,
                YourAcademyVisibility = entry.IsPlayersAcademy ? Visibility.Visible : Visibility.Collapsed,
                Members = entry.Members.Select(m => ToMember(m, noTeamBrush)).ToList(),
                NoMembersVisibility = entry.Members.Any() ? Visibility.Collapsed : Visibility.Visible,
                Former = formerNames.Any() ? string.Format(Strings.AcademiesWindow_Former_Format, string.Join(", ", formerNames)) : string.Empty,
                FormerVisibility = formerNames.Any() ? Visibility.Visible : Visibility.Collapsed
            };
        }

        private static AcademyMemberRow ToMember(AcademyMemberEntry member, Brush noTeamBrush)
        {
            var details = new List<string>();
            if (member.Age.HasValue)
                details.Add(string.Format(Strings.AcademiesWindow_Age_Format, member.Age.Value));
            if (member.AgesOut)
                details.Add(Strings.AcademiesWindow_AgesOut);

            // a reward only lifts a driver without a race seat
            bool inactive = member.Seat is AcademyMemberSeat.RESERVE or AcademyMemberSeat.NO_SEAT;
            bool showOutlook = inactive && member.PracticeOutings > 0;

            return new AcademyMemberRow
            {
                Rank = member.Rank,
                Name = member.Name,
                Detail = string.Join(" · ", details),
                IsPlayer = member.IsPlayer,
                AgesOut = member.AgesOut,
                TeamBrush = ParseColor(member.TeamColor) ?? noTeamBrush,
                SeatTeam = member.Seat == AcademyMemberSeat.NO_SEAT ? Strings.AcademiesWindow_Seat_None : member.TeamShortName ?? member.TeamName ?? string.Empty,
                SeatDetail = member.Seat switch
                {
                    AcademyMemberSeat.PARENT_SEAT => Strings.AcademiesWindow_Seat_Parent,
                    AcademyMemberSeat.SISTER_SEAT => Strings.AcademiesWindow_Seat_Sister,
                    AcademyMemberSeat.OTHER_SEAT => Strings.AcademiesWindow_Seat_Other,
                    AcademyMemberSeat.RESERVE => Strings.AcademiesWindow_Seat_Reserve,
                    _ => string.Empty
                },
                Reputation = SeasonOverviewWindow.FormatReputation(member.Reputation),
                Practice = member.PracticeAverage.HasValue
                    ? string.Format(CultureInfo.CurrentCulture, Strings.AcademiesWindow_Practice_Format, member.PracticeOutings, member.PracticeAverage.Value)
                    : Strings.AcademiesWindow_Practice_None,
                PracticeOutlook = showOutlook ? PracticeOutlook(member.PracticeReward) : string.Empty,
                PracticeOutlookVisibility = showOutlook ? Visibility.Visible : Visibility.Collapsed,
                Season = member.ChampionshipPosition.HasValue
                    ? string.Format(CultureInfo.CurrentCulture, Strings.AcademiesWindow_Season_Format, member.ChampionshipPosition.Value, member.Points ?? 0)
                    : member.Points > 0
                        ? string.Format(CultureInfo.CurrentCulture, Strings.AcademiesWindow_SeasonPoints_Format, member.Points.Value)
                        : Strings.AcademiesWindow_Season_None
            };
        }

        private static string PracticeOutlook(PracticeReward reward) => reward switch
        {
            PracticeReward.YoungChampionshipLevelUnproven => string.Format(Strings.AcademiesWindow_PracticeOutlook_Format, SeasonOverviewWindow.FormatReputation(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN)),
            PracticeReward.YoungTalent => string.Format(Strings.AcademiesWindow_PracticeOutlook_Format, SeasonOverviewWindow.FormatReputation(DriverReputation.YOUNG_TALENT)),
            PracticeReward.NoDecay => Strings.AcademiesWindow_PracticeOutlook_NoDecay,
            _ => Strings.AcademiesWindow_PracticeOutlook_None
        };

        private static Brush ParseColor(string color)
        {
            if (string.IsNullOrEmpty(color))
                return null;

            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
