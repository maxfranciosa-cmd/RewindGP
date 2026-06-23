using Ams2ChEd.Business.AMS2.DependencyInjection;
using AMS2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace AMS2ChEd
{
    public partial class ContractLetterWindow : Window
    {
        private bool isHired;
        private DriverReputation playerReputation;
        private DriverReputation replacedDriverReputation;
        private Ams2Season currentSeason;
        private string playerName;
        private string playerDriverId;
        private string playerNationality;
        private int playerAge;
        private string replacedDriverId;
        private string teamId;
        private IEnumerable<int> favouriteNumbers;
        private Ams2StorageFactory _ams2StorageFactory;
        private GameLogicFactory _gameLogicFactory;

        public ContractLetterWindow(
            Ams2StorageFactory ams2StorageFactory,
            GameLogicFactory gameLogicFactory,
            string teamName,
            string teamId,
            string teamPrincipal,
            string playerName,
            string playerNationality,
            int playerAge,
            string playerDriverId,
            IEnumerable<int> favouriteNumbers,
            DriverReputation playerReputation,
            string replacedDriverName,
            string replacedDriverId,
            DriverReputation replacedDriverReputation,
            string roleName,
            Ams2Season season)
        {
            InitializeComponent();
            this._ams2StorageFactory = ams2StorageFactory;
            this._gameLogicFactory = gameLogicFactory;
            this.playerReputation = playerReputation;
            this.replacedDriverReputation = replacedDriverReputation;
            this.currentSeason = season;
            this.playerName = playerName;
            this.playerDriverId = playerDriverId;
            this.playerNationality = playerNationality;
            this.favouriteNumbers = favouriteNumbers;
            this.playerAge = playerAge;
            this.teamId = teamId;
            this.replacedDriverId = replacedDriverId;
            
            if (playerAge < 18)
            {
                GenerateYoungDriverRejectionLetter(teamName, teamPrincipal, playerName, playerAge);
                NextButton.Visibility = Visibility.Collapsed;
                ChooseAnotherTeamButton.Visibility = Visibility.Visible;
                return;
            }

            if (playerAge > 42 && playerReputation != DriverReputation.JUST_ONE_LAST_DANCE)
            {
                GenerateOlderDriverRejectionLetter(teamName,teamPrincipal, playerName, playerAge);
                NextButton.Visibility = Visibility.Collapsed;
                ChooseAnotherTeamButton.Visibility = Visibility.Visible;
                return;
            }

            // Check if player is hired
            var result = _gameLogicFactory.ContractNegotiationEngine.EvaluateContract(playerDriverId,
                                                                                      playerReputation,
                                                                                      replacedDriverId,
                                                                                      replacedDriverReputation);
            isHired = result.IsPlayerHired;

            if (isHired)
            {
                GenerateSuccessLetter(teamName, teamPrincipal, playerName, replacedDriverName, playerReputation, roleName);
                NextButton.Visibility = Visibility.Visible;
                ChooseAnotherTeamButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                GenerateRejectionLetter(teamName, teamPrincipal, playerName, replacedDriverName, roleName);
                NextButton.Visibility = Visibility.Collapsed;
                ChooseAnotherTeamButton.Visibility = Visibility.Visible;
            }
        }

        private void GenerateYoungDriverRejectionLetter(string teamName, string teamPrincipal, string playerName, int driverAge)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            string letterText;

            if (driverAge <= 6)
            {
                // Very young children - pure dream stage
                letterText = $"Dear {playerName},\n\n" +
                            $"Thank you for your wonderful letter to {teamName}! It made everyone in our office smile to hear from such a young racing fan.\n\n" +
                            $"We think it's absolutely brilliant that you dream of being a racing driver! At {driverAge} years old, you have so much time to learn and grow. Right now, the best thing you can do is:\n\n" +
                            $"Watch lots of races with your family, play with your toy racing cars and learn how they work, draw pictures of racing cars and tracks, and most importantly, work hard at school because racing drivers need to be smart too!\n\n" +
                            $"When you're a bit older, maybe you can try go-karting with your parents. That's how all the best drivers start their journey.\n\n" +
                            $"Keep dreaming big, {playerName}. Who knows? Maybe one day we'll see you on the starting grid!\n\n" +
                            $"Your friend in racing,";
            }
            else if (driverAge <= 14)
            {
                // Karting age - developmental stage
                letterText = $"Dear {playerName},\n\n" +
                            $"Thank you so much for your letter expressing interest in driving for {teamName}. It's wonderful to see young people like yourself showing such enthusiasm for motor racing at {driverAge} years old!\n\n" +
                            $"Your passion for the sport is truly inspiring, and I want you to know that we take every letter we receive very seriously. However, Formula One racing requires drivers to be at least 18 years of age to hold a Super Licence from the FIA, which is necessary to compete in our championship.\n\n" +
                            $"This doesn't mean you should give up on your dream! Right now, at your age, you should be focusing on karting if you haven't already started. Karting is where every Formula One driver begins - it teaches you racecraft, car control, and how to compete wheel-to-wheel. If you're already karting, keep pushing yourself and try to compete at the highest level you can.\n\n" +
                            $"Work hard at school too - many successful drivers have strong technical knowledge that helps them understand their cars better. Stay fit and healthy, watch every race you can, and learn from the best drivers on the grid.\n\n" +
                            $"In a few years, if you're winning races and showing real talent, perhaps we'll hear your name coming up through the junior categories. That's when teams like ours start paying attention.\n\n" +
                            $"Keep that dream alive, {playerName}. Formula One will be here waiting for you when you're ready.\n\n" +
                            $"With warmest regards and best wishes for your racing future,";
            }
            else // 14-17
            {
                // Junior formula age - serious development stage
                letterText = $"Dear {playerName},\n\n" +
                            $"Thank you for your application to drive for {teamName}. At {driverAge} years old, you're at a crucial stage in any racing driver's career development.\n\n" +
                            $"While I admire your ambition in reaching out to us directly, I must be honest with you about the path ahead. Formula One requires drivers to hold an FIA Super Licence, which isn't available until you're 18 years of age. However, the next few years are absolutely critical for your development.\n\n" +
                            $"If you're serious about reaching Formula One, you should be competing in junior single-seater categories right now - Formula 4, Formula Regional, or working your way toward Formula 3 and Formula 2. This is where we scout talent. We're watching these championships closely, looking for drivers who consistently perform at the front, who show maturity under pressure, and who demonstrate the technical understanding needed at this level.\n\n" +
                            $"My advice to you is this: focus entirely on your current racing program. Win races, score podiums, and make people notice your name. Build relationships with teams in the junior categories. Work with your coaches on every aspect of your driving - physical fitness, mental preparation, technical feedback, and racecraft.\n\n" +
                            $"If you can prove yourself in the junior formulas over the next few years, and if you show the results and maturity we're looking for, then we might very well be having a different conversation when you turn 18.\n\n" +
                            $"The door isn't closed - it's just not open yet. Make the most of these crucial years.\n\n" +
                            $"Best of luck with your racing career,";
            }

            LetterContent.Text = letterText;

            // Set signature with age-appropriate tone
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = $"Team Principal, {teamName}";
        }

        private void GenerateOlderDriverRejectionLetter(string teamName, string teamPrincipal, string playerName, int driverAge)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Generate personalized message for older driver
            string letterText = $"Dear {playerName},\n\n" +
                              $"Thank you for your application to drive for {teamName}. I must say, receiving an application from someone of your experience at {driverAge} years of age is certainly unique in this day and age.\n\n" +
                              $"While I admire your obvious passion for motor racing and your determination to pursue a Formula One career, I must be frank with you. Formula One is an extraordinarily demanding sport, both physically and mentally. The forces experienced during braking, cornering, and acceleration are immense, and the reaction times required are at the absolute limit of human capability.\n\n" +
                              $"Our data suggests that drivers typically reach their physical peak performance in their late twenties to early thirties. By {driverAge}, even the most exceptional athletes begin to experience the natural effects of aging on reaction time, sustained concentration, and physical endurance in the cockpit.\n\n" +
                              $"Additionally, our young driver program and current roster are focused on developing talent with longer-term career potential. As a professional racing team, we must make decisions based not just on immediate capability, but on building relationships that can span many seasons.\n\n" +
                              $"I don't want to discourage you from motorsport entirely - there are many other categories where age is less of a limiting factor. Historic racing, endurance events, and gentleman driver categories can be extremely rewarding and competitive.\n\n" +
                              $"I wish you the very best in whatever motorsport endeavors you pursue.";

            LetterContent.Text = letterText;

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = $"Team Principal, {teamName}";
        }

        private void GenerateSuccessLetter(string teamName, string teamPrincipal, string playerName, string replacedDriverName, DriverReputation playerReputation, string roleName)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Generate personalized message based on reputation
            string reputationReason = GetReputationReason(playerReputation);
            string competitionMention = GetCompetitionMention(replacedDriverName);

            string letterText = $"Dear {playerName},\n\n" +
                              $"On behalf of {teamName}, I am delighted to inform you that we have decided to offer you a position as {roleName} for our team.\n\n" +
                              $"{competitionMention}\n\n" +
                              $"{reputationReason}\n\n" +
                              $"We believe you have what it takes to represent {teamName} with pride and determination. " +
                              $"This is an incredible opportunity to prove yourself at the highest level of motorsport, and we are confident you will rise to the challenge.\n\n" +
                              $"We look forward to seeing you behind the wheel and achieving great things together.\n\n" +
                              $"Welcome to the team!";

            LetterContent.Text = letterText;

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = $"Team Principal, {teamName}";
        }

        private string GetReputationReason(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    "While we recognize that your budget brings valuable resources to our team, we also see potential in your raw talent. This opportunity allows you to demonstrate your abilities when it matters most.",

                DriverReputation.PAY_DRIVER_SEASON =>
                    "Your financial backing has secured you this seat, but we expect you to prove that you deserve it through your performance on track. Show us what you're capable of.",

                DriverReputation.YOUNG_TALENT =>
                    "Your recent performances have caught our attention. While you're still developing as a driver, we see tremendous potential in you. We're willing to take a chance on your raw talent and give you the opportunity to learn and grow with us.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    "You've shown flashes of brilliance that suggest you could be championship material. However, you haven't yet proven you can sustain that level consistently. We believe our team can provide the environment for you to take that final step.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    "Your accomplishments speak for themselves. You've proven you have the talent to fight for championships, and we want that winning mentality in our garage. We believe together we can achieve great success.",

                DriverReputation.PRIME_MIDFIELD =>
                    "Your consistency and reliability are exactly what our team needs. You may not grab the headlines, but you deliver solid results race after race, and that's invaluable to us.",

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    "You've consistently punched above your weight in the midfield, and we believe you're ready for the next challenge. Your ability to maximize every opportunity makes you an ideal candidate for our team.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    "You've demonstrated race-winning pace, but the championship has eluded you so far. We believe our team can provide the platform for you to finally challenge for the title you deserve.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    "As a proven champion, you bring the winning experience and mentality we need. Your track record speaks for itself, and we're honored to have you join our team.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    "While some might say your best days are behind you, we see a champion hungry to prove the doubters wrong. Your experience and determination make you the perfect fit for our team.",

                DriverReputation.AGEING_MIDFIELD =>
                    "Your years of experience make you a safe pair of hands. In our position, reliability and consistency are exactly what we need, and you deliver that every weekend.",

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    "Your veteran experience combined with your continued strong performances make you an ideal addition to our lineup. You know how to extract the maximum from the car.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    "Age is just a number, and you continue to prove you can compete at the highest level. Your experience and racecraft are invaluable assets to our team.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    "You may have fallen from your peak, but we believe you still have the fire within. We're giving you the chance to recapture past glory and silence your critics.",

                _ => "Your dedication and passion for racing have impressed us, and we believe you deserve this opportunity."
            };
        }

        private string GetCompetitionMention(string replacedDriverName)
        {
            return $"The decision was not easy. We carefully considered both yourself and {replacedDriverName} for this position. " +
                   $"After extensive deliberation, we felt that you were the better fit for what we're trying to achieve.";
        }

        private void GenerateRejectionLetter(string teamName, string teamPrincipal, string playerName, string replacedDriverName, string roleName)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Get rejection reason based on reputation
            string rejectionReason = GetRejectionReason(playerReputation);
            string preferredDriverReason = GetPreferredDriverReason(replacedDriverReputation, replacedDriverName);

            string letterText = $"Dear {playerName},\n\n" +
                              $"Thank you for your interest in joining {teamName} as {roleName}. We truly appreciate the time and effort you invested in pursuing this opportunity with us.\n\n" +
                              $"After careful consideration of all candidates, including yourself and {replacedDriverName}, we have made the difficult decision to continue with our current lineup. " +
                              $"While we were impressed by certain aspects of your profile, we ultimately felt that {replacedDriverName} better aligns with our team's current needs and direction.\n\n" +
                              $"{rejectionReason}\n\n" +
                              $"{preferredDriverReason}\n\n" +
                              $"We wish you the very best in your career and hope that our paths may cross again in the future under different circumstances.\n\n" +
                              $"Best regards,";

            LetterContent.Text = letterText;

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = $"Team Principal, {teamName}";
        }

        private string GetRejectionReason(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    "While your financial backing is appreciated, we felt that the lack of a full-season commitment and proven track record made this a difficult proposition for us at this time.",

                DriverReputation.PAY_DRIVER_SEASON =>
                    "Although your financial support was certainly a factor in our consideration, we ultimately prioritized proven performance and experience for this particular seat.",

                DriverReputation.YOUNG_TALENT =>
                    "Your raw talent is undeniable, but we felt that the risks associated with an inexperienced driver were too high for our current situation. We need someone who can deliver consistent results immediately.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    "While you've shown flashes of brilliance, we need a driver who has proven they can perform at this level consistently. The pressure of our team requires someone with a more established track record.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    "This was an exceptionally difficult decision. Your championship credentials speak for themselves, but we felt that our current driver's experience with the team gives us a slight edge going forward.",

                DriverReputation.PRIME_MIDFIELD =>
                    "Your reliability is commendable, but we're looking for a driver who can push beyond solid midfield performances. We need someone who can occasionally deliver exceptional results.",

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    "You're a quality midfield driver, but we felt our current driver offers slightly more upside and experience that better fits our immediate goals.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    "While your race-winning capabilities are impressive, we need a driver who has proven they can sustain championship-level performance over a full season.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    "This decision came down to the finest of margins. Both you and our current driver are championship caliber, but we felt continuity with our existing lineup was the best path forward.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    "While we respect your championship pedigree, we have concerns about whether you can recapture your previous form. At this stage, we need a driver who is consistently at their peak.",

                DriverReputation.AGEING_MIDFIELD =>
                    "Your experience is valuable, but we're looking for a driver who can offer more than just reliability. We need someone who can maximize every opportunity for points.",

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    "While your veteran presence would have been beneficial, we ultimately felt that our current driver offers better long-term potential for the team.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    "This was a close call. Your experience and racecraft are exceptional, but we opted to stick with our current driver who has more years ahead in the sport.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    "While we admire your determination to return to winning ways, we felt that the risk of declining performance outweighed the potential benefits of your experience.",

                _ => "After weighing all factors, we concluded that our current driver was the better fit for the team at this time."
            };
        }

        private string GetPreferredDriverReason(DriverReputation replacedReputation, string replacedDriverName)
        {
            return replacedReputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    $"{replacedDriverName}'s established relationships with our sponsors and their proven ability to integrate quickly into our operations made them the more attractive option.",

                DriverReputation.PAY_DRIVER_SEASON =>
                    $"{replacedDriverName}'s existing commercial partnerships and their demonstrated commitment to our team's vision aligned better with our objectives.",

                DriverReputation.YOUNG_TALENT =>
                    $"{replacedDriverName} has already shown they understand our team's philosophy and have developed strong working relationships with our technical staff.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    $"{replacedDriverName}'s track record with us and their growing chemistry with our engineering team represented less risk and more immediate potential.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    $"{replacedDriverName}'s proven abilities combined with their existing integration into our team structure offered us the best path forward.",

                DriverReputation.PRIME_MIDFIELD =>
                    $"{replacedDriverName}'s deep understanding of our car and their established communication channels with our engineers gave them a clear advantage.",

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    $"{replacedDriverName}'s experience with our machinery and their track record of delivering results in our colors made them the logical choice.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    $"{replacedDriverName}'s technical contributions to our car development and their demonstrated ability to extract performance from our package tipped the balance.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    $"{replacedDriverName}'s leadership within our team and their proven success in our environment made changing drivers an unnecessary risk.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    $"{replacedDriverName}'s intimate knowledge of our team's strengths and their motivation to succeed with us specifically convinced us to maintain continuity.",

                DriverReputation.AGEING_MIDFIELD =>
                    $"{replacedDriverName}'s wealth of experience with our specific car characteristics and their seamless integration into our workflow was difficult to overlook.",

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    $"{replacedDriverName}'s accumulated knowledge of our team's operations and their consistent ability to maximize our package represented proven value.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    $"{replacedDriverName}'s extensive experience and their established role as a leader within our organization extended their value beyond pure driving performance.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    $"{replacedDriverName}'s deep connection with our team culture and their unwavering commitment to our success made them the preferred candidate.",

                _ => $"{replacedDriverName}'s proven track record with our team and their ability to work effectively within our structure ultimately gave them the edge."
            };
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void ChooseAnotherTeamButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}