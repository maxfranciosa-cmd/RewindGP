using System.Resources;

namespace Ams2ChEd.Business.AMS2.Resources
{
    /// <summary>
    /// Hand-written accessor over Strings.resx (+ Strings.&lt;culture&gt;.resx satellites, e.g.
    /// Strings.it.resx). Not designer-generated: `dotnet build` alone never invokes the ResX
    /// single-file generator (that's a Visual Studio design-time-only feature), so a checked-in
    /// Strings.Designer.cs would silently go stale in CI/CLI builds. Keep the property list below
    /// and the resx &lt;data name="..."&gt; keys in sync by hand - see
    /// Ams2ChEd.Business.AMS2/Localization/Loc.cs for the lookup/fallback logic that reads these at
    /// runtime. Mirrors AMS2ChEd.Resources.Strings in the main app project; this project has no
    /// reference to that one (dependency points the other way), so it needs its own copy.
    /// </summary>
    public static class Strings
    {
        public static ResourceManager ResourceManager { get; } =
            new ResourceManager("Ams2ChEd.Business.AMS2.Resources.Strings", typeof(Strings).Assembly);

        public static string Ams2ProgressWindow_Title => ResourceManager.GetString(nameof(Ams2ProgressWindow_Title))!;
        public static string Ams2ProgressWindow_DefaultMessage => ResourceManager.GetString(nameof(Ams2ProgressWindow_DefaultMessage))!;

        public static string OptionsWindow_Title => ResourceManager.GetString(nameof(OptionsWindow_Title))!;
        public static string OptionsWindow_HeaderText => ResourceManager.GetString(nameof(OptionsWindow_HeaderText))!;
        public static string OptionsWindow_Ams2FolderLabel => ResourceManager.GetString(nameof(OptionsWindow_Ams2FolderLabel))!;
        public static string OptionsWindow_BrowseButton => ResourceManager.GetString(nameof(OptionsWindow_BrowseButton))!;
        public static string OptionsWindow_InGamePlayerNameLabel => ResourceManager.GetString(nameof(OptionsWindow_InGamePlayerNameLabel))!;
        public static string OptionsWindow_RaceLengthLabel => ResourceManager.GetString(nameof(OptionsWindow_RaceLengthLabel))!;
        public static string OptionsWindow_RaceLength_Default => ResourceManager.GetString(nameof(OptionsWindow_RaceLength_Default))!;
        public static string OptionsWindow_RaceLength_OneThird => ResourceManager.GetString(nameof(OptionsWindow_RaceLength_OneThird))!;
        public static string OptionsWindow_RaceLength_Half => ResourceManager.GetString(nameof(OptionsWindow_RaceLength_Half))!;
        public static string OptionsWindow_RaceLength_Full => ResourceManager.GetString(nameof(OptionsWindow_RaceLength_Full))!;
        public static string OptionsWindow_LanguageLabel => ResourceManager.GetString(nameof(OptionsWindow_LanguageLabel))!;
        public static string OptionsWindow_SaveButton => ResourceManager.GetString(nameof(OptionsWindow_SaveButton))!;
        public static string OptionsWindow_RestoreVehicleFilesButton => ResourceManager.GetString(nameof(OptionsWindow_RestoreVehicleFilesButton))!;
        public static string OptionsWindow_CloseButton => ResourceManager.GetString(nameof(OptionsWindow_CloseButton))!;
        public static string OptionsWindow_BrowseDialog_Description => ResourceManager.GetString(nameof(OptionsWindow_BrowseDialog_Description))!;

        public static string OptionsWindow_FolderPathRequired_Title => ResourceManager.GetString(nameof(OptionsWindow_FolderPathRequired_Title))!;
        public static string OptionsWindow_FolderPathRequired_Message => ResourceManager.GetString(nameof(OptionsWindow_FolderPathRequired_Message))!;
        public static string OptionsWindow_FolderNotFound_Title => ResourceManager.GetString(nameof(OptionsWindow_FolderNotFound_Title))!;
        public static string OptionsWindow_FolderNotFound_Message => ResourceManager.GetString(nameof(OptionsWindow_FolderNotFound_Message))!;
        public static string OptionsWindow_PlayerNameRequired_Title => ResourceManager.GetString(nameof(OptionsWindow_PlayerNameRequired_Title))!;
        public static string OptionsWindow_PlayerNameRequired_Message => ResourceManager.GetString(nameof(OptionsWindow_PlayerNameRequired_Message))!;
        public static string OptionsWindow_SettingsSaved_Title => ResourceManager.GetString(nameof(OptionsWindow_SettingsSaved_Title))!;
        public static string OptionsWindow_SettingsSaved_Message => ResourceManager.GetString(nameof(OptionsWindow_SettingsSaved_Message))!;
        public static string OptionsWindow_LanguageChangeRequiresRestart_Title => ResourceManager.GetString(nameof(OptionsWindow_LanguageChangeRequiresRestart_Title))!;
        public static string OptionsWindow_LanguageChangeRequiresRestart_Message => ResourceManager.GetString(nameof(OptionsWindow_LanguageChangeRequiresRestart_Message))!;
        public static string OptionsWindow_SaveError_Title => ResourceManager.GetString(nameof(OptionsWindow_SaveError_Title))!;
        public static string OptionsWindow_SaveError_Message => ResourceManager.GetString(nameof(OptionsWindow_SaveError_Message))!;
        public static string OptionsWindow_NothingToRestore_Title => ResourceManager.GetString(nameof(OptionsWindow_NothingToRestore_Title))!;
        public static string OptionsWindow_NothingToRestore_Message => ResourceManager.GetString(nameof(OptionsWindow_NothingToRestore_Message))!;
        public static string OptionsWindow_RestoreConfirm_Title => ResourceManager.GetString(nameof(OptionsWindow_RestoreConfirm_Title))!;
        public static string OptionsWindow_RestoreConfirm_Message => ResourceManager.GetString(nameof(OptionsWindow_RestoreConfirm_Message))!;
        public static string OptionsWindow_RestoringFiles_Message => ResourceManager.GetString(nameof(OptionsWindow_RestoringFiles_Message))!;
        public static string OptionsWindow_RestoreComplete_Title => ResourceManager.GetString(nameof(OptionsWindow_RestoreComplete_Title))!;
        public static string OptionsWindow_RestoreComplete_Message => ResourceManager.GetString(nameof(OptionsWindow_RestoreComplete_Message))!;
        public static string OptionsWindow_RestoreFailed_Title => ResourceManager.GetString(nameof(OptionsWindow_RestoreFailed_Title))!;
        public static string OptionsWindow_RestoreFailed_Message => ResourceManager.GetString(nameof(OptionsWindow_RestoreFailed_Message))!;
    }
}
