using AMS2ChEd.Resources;
using AMS2ChEd.Views.Letters;
using System.Windows;
using System.Windows.Controls;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The sheet every letter is written on, so they all look the same. <see cref="LetterWindow"/>
    /// fills it from a <see cref="Letter"/>; a window with a letter of its own (AcademyLetterWindow)
    /// sets the properties itself and puts its controls in <see cref="ExtraContent"/>.
    /// </summary>
    public partial class LetterPaper : UserControl
    {
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(LetterPaper), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TaglineProperty =
            DependencyProperty.Register(nameof(Tagline), typeof(string), typeof(LetterPaper), new PropertyMetadata(null));

        public static readonly DependencyProperty SubjectProperty =
            DependencyProperty.Register(nameof(Subject), typeof(string), typeof(LetterPaper), new PropertyMetadata(null, OnSubjectChanged));

        public static readonly DependencyProperty BodyProperty =
            DependencyProperty.Register(nameof(Body), typeof(string), typeof(LetterPaper), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SignoffProperty =
            DependencyProperty.Register(nameof(Signoff), typeof(string), typeof(LetterPaper), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SignatureNameProperty =
            DependencyProperty.Register(nameof(SignatureName), typeof(string), typeof(LetterPaper), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SignatureTitleProperty =
            DependencyProperty.Register(nameof(SignatureTitle), typeof(string), typeof(LetterPaper), new PropertyMetadata(null));

        public static readonly DependencyProperty ExtraContentProperty =
            DependencyProperty.Register(nameof(ExtraContent), typeof(object), typeof(LetterPaper), new PropertyMetadata(null));

        public LetterPaper()
        {
            InitializeComponent();
            LayoutRoot.DataContext = this;
            Signoff = Strings.Letter_Signoff;
            UpdateSubjectLine();
        }

        public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public string? Tagline { get => (string?)GetValue(TaglineProperty); set => SetValue(TaglineProperty, value); }
        public string? Subject { get => (string?)GetValue(SubjectProperty); set => SetValue(SubjectProperty, value); }
        public string Body { get => (string)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
        public string Signoff { get => (string)GetValue(SignoffProperty); set => SetValue(SignoffProperty, value); }
        public string SignatureName { get => (string)GetValue(SignatureNameProperty); set => SetValue(SignatureNameProperty, value); }
        public string? SignatureTitle { get => (string?)GetValue(SignatureTitleProperty); set => SetValue(SignatureTitleProperty, value); }
        public object? ExtraContent { get => GetValue(ExtraContentProperty); set => SetValue(ExtraContentProperty, value); }

        /// <summary>Writes the letter on the paper, from the top of the sheet.</summary>
        public void Show(Letter letter)
        {
            Header = letter.Header;
            Tagline = letter.Tagline;
            Subject = letter.Subject;
            Body = letter.Body;
            Signoff = letter.Signoff;
            SignatureName = letter.SignatureName;
            SignatureTitle = letter.SignatureTitle;
            Scroller.ScrollToTop();
        }

        private static void OnSubjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((LetterPaper)d).UpdateSubjectLine();

        // a letter without a subject shows no "Subject:" line at all
        private void UpdateSubjectLine()
        {
            if (SubjectLine != null)
                SubjectLine.Visibility = string.IsNullOrEmpty(Subject) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
