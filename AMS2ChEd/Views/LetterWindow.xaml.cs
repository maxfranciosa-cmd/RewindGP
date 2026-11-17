using AMS2ChEd.Views.Letters;
using System.Windows;
using System.Windows.Controls;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Shows any <see cref="Letter"/> and closes with the id of the button the player clicked
    /// (<see cref="ChosenActionId"/>; null when the window was closed without one). A button with a
    /// follow-up letter turns the page instead of closing.
    /// </summary>
    public partial class LetterWindow : Window
    {
        public string? ChosenActionId { get; private set; }

        public LetterWindow(Letter letter)
        {
            InitializeComponent();
            ShowLetter(letter);
        }

        /// <summary>Shows the letter as a dialog and returns the id of the button clicked.</summary>
        public static string? Present(Window? owner, Letter letter)
        {
            var window = new LetterWindow(letter);
            if (owner != null)
                window.Owner = owner;
            window.ShowDialog();
            return window.ChosenActionId;
        }

        private void ShowLetter(Letter letter)
        {
            Title = letter.Subject;
            Paper.Show(letter);

            ButtonPanel.Children.Clear();
            foreach (var action in letter.Actions)
            {
                var button = new System.Windows.Controls.Button
                {
                    Content = action.Label,
                    Style = (Style)FindResource(StyleKey(action.Kind))
                };
                button.Click += (_, _) => OnAction(action);
                ButtonPanel.Children.Add(button);
            }
        }

        private void OnAction(LetterAction action)
        {
            if (action.Next != null)
            {
                ShowLetter(action.Next());
                return;
            }

            ChosenActionId = action.Id;
            DialogResult = true;
            Close();
        }

        private static string StyleKey(LetterActionKind kind) => kind switch
        {
            LetterActionKind.Accept => "LetterAcceptButtonStyle",
            LetterActionKind.Reject => "LetterRejectButtonStyle",
            LetterActionKind.Neutral => "LetterNeutralButtonStyle",
            _ => "LetterPrimaryButtonStyle"
        };
    }
}
