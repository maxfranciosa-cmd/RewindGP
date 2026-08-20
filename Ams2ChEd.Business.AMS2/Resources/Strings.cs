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

        public static string PrerequisiteSetupWindow_Title => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Title))!;
        public static string PrerequisiteSetupWindow_Header => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Header))!;
        public static string PrerequisiteSetupWindow_Intro => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Intro))!;
        public static string PrerequisiteSetupWindow_Step1Header => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Step1Header))!;
        public static string PrerequisiteSetupWindow_Step1Body => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Step1Body))!;
        public static string PrerequisiteSetupWindow_Step2Header => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Step2Header))!;
        public static string PrerequisiteSetupWindow_Step2Body => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_Step2Body))!;
        public static string PrerequisiteSetupWindow_DontShowAgain => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_DontShowAgain))!;
        public static string PrerequisiteSetupWindow_GotItButton => ResourceManager.GetString(nameof(PrerequisiteSetupWindow_GotItButton))!;

        public static string RaceReturnOverlayWindow_SessionFinished => ResourceManager.GetString(nameof(RaceReturnOverlayWindow_SessionFinished))!;
        public static string RaceReturnOverlayWindow_ReturnButton => ResourceManager.GetString(nameof(RaceReturnOverlayWindow_ReturnButton))!;

        public static string RaceSetupOverlayWindow_PromptText => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_PromptText))!;
        public static string RaceSetupOverlayWindow_ConfigureButton => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ConfigureButton))!;
        public static string RaceSetupOverlayWindow_SkipLink => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_SkipLink))!;
        public static string RaceSetupOverlayWindow_WaitingText => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_WaitingText))!;
        public static string RaceSetupOverlayWindow_LaunchingText => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_LaunchingText))!;
        public static string RaceSetupOverlayWindow_SuccessText => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_SuccessText))!;
        public static string RaceSetupOverlayWindow_SuccessOkButton => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_SuccessOkButton))!;
        public static string RaceSetupOverlayWindow_ContinueManuallyButton => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ContinueManuallyButton))!;
        public static string RaceSetupOverlayWindow_ManualSetupTitle => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualSetupTitle))!;
        public static string RaceSetupOverlayWindow_PreQualiSessionTitle => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_PreQualiSessionTitle))!;
        public static string RaceSetupOverlayWindow_ManualInstructionsOkButton => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualInstructionsOkButton))!;
        public static string RaceSetupOverlayWindow_ManualStep1_Format => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualStep1_Format))!;
        public static string RaceSetupOverlayWindow_ManualStep2_Format => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualStep2_Format))!;
        public static string RaceSetupOverlayWindow_ManualStep3_UsesScalars => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualStep3_UsesScalars))!;
        public static string RaceSetupOverlayWindow_ManualStep3_Format => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualStep3_Format))!;
        public static string RaceSetupOverlayWindow_ManualStep4_PreQuali => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualStep4_PreQuali))!;
        public static string RaceSetupOverlayWindow_ManualStep4_Normal => ResourceManager.GetString(nameof(RaceSetupOverlayWindow_ManualStep4_Normal))!;

        public static string Ams2RaceLaunchAssistant_ProcessLost => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_ProcessLost))!;
        public static string Ams2RaceLaunchAssistant_RaceNotFound => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_RaceNotFound))!;
        public static string Ams2RaceLaunchAssistant_TrackNotConfigured_Format => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_TrackNotConfigured_Format))!;
        public static string Ams2RaceLaunchAssistant_TrackNotInCatalog_Format => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_TrackNotInCatalog_Format))!;
        public static string Ams2RaceLaunchAssistant_CarSelectionFailed => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_CarSelectionFailed))!;
        public static string Ams2RaceLaunchAssistant_CarNotInCatalog_Format => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_CarNotInCatalog_Format))!;
        public static string Ams2RaceLaunchAssistant_AttachFailed => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_AttachFailed))!;
        public static string Ams2RaceLaunchAssistant_ApplyFailed => ResourceManager.GetString(nameof(Ams2RaceLaunchAssistant_ApplyFailed))!;

        public static string Ams2PlayerCosmeticsEditorWindow_Title => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_Title))!;
        public static string Ams2PlayerCosmeticsEditorWindow_Header => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_Header))!;
        public static string Ams2PlayerCosmeticsEditorWindow_PlayerNameLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_PlayerNameLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_NationalityLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_NationalityLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_NationalityHelp => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_NationalityHelp))!;
        public static string Ams2PlayerCosmeticsEditorWindow_PhotoFileLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_PhotoFileLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_PhotoFileHelp => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_PhotoFileHelp))!;
        public static string Ams2PlayerCosmeticsEditorWindow_BrowseButton => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_BrowseButton))!;
        public static string Ams2PlayerCosmeticsEditorWindow_PhotoUrlLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_PhotoUrlLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_PhotoUrlHelp => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_PhotoUrlHelp))!;
        public static string Ams2PlayerCosmeticsEditorWindow_HelmetDesignLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_HelmetDesignLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_ModernTab => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_ModernTab))!;
        public static string Ams2PlayerCosmeticsEditorWindow_NinetiesTab => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_NinetiesTab))!;
        public static string Ams2PlayerCosmeticsEditorWindow_EightiesTab => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_EightiesTab))!;
        public static string Ams2PlayerCosmeticsEditorWindow_SeventiesTab => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_SeventiesTab))!;
        public static string Ams2PlayerCosmeticsEditorWindow_UseDefaultHelmetOption => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_UseDefaultHelmetOption))!;
        public static string Ams2PlayerCosmeticsEditorWindow_UseCustomFilesOption => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_UseCustomFilesOption))!;
        public static string Ams2PlayerCosmeticsEditorWindow_SelectHelmetDesignLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_SelectHelmetDesignLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_HelmetFileLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_HelmetFileLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_VisorFileLabel => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_VisorFileLabel))!;
        public static string Ams2PlayerCosmeticsEditorWindow_NinetiesNoVisorNote => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_NinetiesNoVisorNote))!;
        public static string Ams2PlayerCosmeticsEditorWindow_CancelButton => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_CancelButton))!;
        public static string Ams2PlayerCosmeticsEditorWindow_SaveButton => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_SaveButton))!;
        public static string Ams2PlayerCosmeticsEditorWindow_SelectPhotoTitle => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_SelectPhotoTitle))!;
        public static string Ams2PlayerCosmeticsEditorWindow_ImageFileFilter => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_ImageFileFilter))!;
        public static string Ams2PlayerCosmeticsEditorWindow_SelectHelmetTextureTitle => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_SelectHelmetTextureTitle))!;
        public static string Ams2PlayerCosmeticsEditorWindow_SelectVisorTextureTitle => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_SelectVisorTextureTitle))!;
        public static string Ams2PlayerCosmeticsEditorWindow_TextureFileFilter => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_TextureFileFilter))!;
        public static string Ams2PlayerCosmeticsEditorWindow_NameRequired_Message => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_NameRequired_Message))!;
        public static string Ams2PlayerCosmeticsEditorWindow_ValidationError_Title => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_ValidationError_Title))!;
        public static string Ams2PlayerCosmeticsEditorWindow_NationalityLength_Message => ResourceManager.GetString(nameof(Ams2PlayerCosmeticsEditorWindow_NationalityLength_Message))!;
    }
}
