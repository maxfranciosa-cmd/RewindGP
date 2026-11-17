using AMS2ChEd.Resources;

namespace AMS2ChEd.Views.Letters
{
    /// <summary>How a letter button looks: the colour of the choice it stands for.</summary>
    public enum LetterActionKind
    {
        // the usual way on (NEXT): red
        Primary,
        // taking what is offered: green
        Accept,
        // turning it down: red
        Reject,
        // nothing to choose, or a way back: dark
        Neutral
    }

    /// <summary>
    /// One button under a letter. Clicking it closes the letter with its <see cref="Id"/>, unless
    /// it has a <see cref="Next"/> letter: then the same window carries on with that one.
    /// </summary>
    public sealed class LetterAction
    {
        public string Id { get; }
        public string Label { get; }
        public LetterActionKind Kind { get; }
        public Func<Letter>? Next { get; }

        public LetterAction(string id, string label, LetterActionKind kind, Func<Letter>? next = null)
        {
            Id = id;
            Label = label;
            Kind = kind;
            Next = next;
        }
    }

    /// <summary>The ids letters close with, for callers to turn back into a decision.</summary>
    public static class LetterActionIds
    {
        public const string Next = "next";
        public const string Accept = "accept";
        public const string Reject = "reject";
        public const string Continue = "continue";
        public const string ChooseAnotherTeam = "choose_another_team";
    }

    /// <summary>
    /// Everything a letter shows: who it's from (letterhead and signature), what it's about (the
    /// subject), the text, and the buttons under it. Built by the composers in this folder and shown
    /// by <see cref="LetterWindow"/>, or on a <see cref="LetterPaper"/> by a window of its own.
    /// </summary>
    public sealed class Letter
    {
        public string Header { get; init; } = string.Empty;
        public string? Tagline { get; init; }
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Signoff { get; init; } = Strings.Letter_Signoff;
        public string SignatureName { get; init; } = string.Empty;
        public string? SignatureTitle { get; init; }
        public IReadOnlyList<LetterAction> Actions { get; init; } = Array.Empty<LetterAction>();

        /// <summary>A letter on the team's letterhead, signed by its team principal.</summary>
        public static Letter FromTeam(string teamName, string teamPrincipal, string subject, string body, params LetterAction[] actions)
        {
            return new Letter
            {
                Header = (teamName ?? string.Empty).ToUpper(),
                Tagline = Strings.Letter_TeamTagline,
                Subject = subject,
                Body = body,
                SignatureName = teamPrincipal ?? string.Empty,
                SignatureTitle = string.Format(Strings.Letter_SignatureTitle_Format, teamName),
                Actions = actions
            };
        }

        public static LetterAction NextAction() =>
            new LetterAction(LetterActionIds.Next, Strings.Letter_NextButton, LetterActionKind.Primary);

        public static LetterAction ContinueAction() =>
            new LetterAction(LetterActionIds.Continue, Strings.Letter_ContinueButton, LetterActionKind.Neutral);
    }
}
