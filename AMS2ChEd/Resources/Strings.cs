using System.Resources;

namespace AMS2ChEd.Resources
{
    /// <summary>
    /// Hand-written accessor over Strings.resx (+ Strings.&lt;culture&gt;.resx satellites, e.g.
    /// Strings.it.resx). Not designer-generated: `dotnet build` alone never invokes the ResX
    /// single-file generator (that's a Visual Studio design-time-only feature), so a checked-in
    /// Strings.Designer.cs would silently go stale in CI/CLI builds. Keep the property list below
    /// and the resx &lt;data name="..."&gt; keys in sync by hand - see AMS2ChEd/Localization/Loc.cs
    /// for the lookup/fallback logic that actually reads these at runtime.
    /// </summary>
    public static class Strings
    {
        public static ResourceManager ResourceManager { get; } =
            new ResourceManager("AMS2ChEd.Resources.Strings", typeof(Strings).Assembly);

        public static string ProgressWindow_Title => ResourceManager.GetString(nameof(ProgressWindow_Title))!;
        public static string ProgressWindow_DefaultMessage => ResourceManager.GetString(nameof(ProgressWindow_DefaultMessage))!;

        public static string MainWindow_Title => ResourceManager.GetString(nameof(MainWindow_Title))!;
        public static string MainWindow_NewGameButton => ResourceManager.GetString(nameof(MainWindow_NewGameButton))!;
        public static string MainWindow_NewGameReplaceDriverButton => ResourceManager.GetString(nameof(MainWindow_NewGameReplaceDriverButton))!;
        public static string MainWindow_NewGameScenarioModeButton => ResourceManager.GetString(nameof(MainWindow_NewGameScenarioModeButton))!;
        public static string MainWindow_LoadGameButton => ResourceManager.GetString(nameof(MainWindow_LoadGameButton))!;
        public static string MainWindow_OptionsButton => ResourceManager.GetString(nameof(MainWindow_OptionsButton))!;
        public static string MainWindow_InstallSeasonModButton => ResourceManager.GetString(nameof(MainWindow_InstallSeasonModButton))!;
        public static string MainWindow_DeveloperToolsButton => ResourceManager.GetString(nameof(MainWindow_DeveloperToolsButton))!;
        public static string MainWindow_SelectSeasonLabel => ResourceManager.GetString(nameof(MainWindow_SelectSeasonLabel))!;
        public static string MainWindow_SelectRaceLabel => ResourceManager.GetString(nameof(MainWindow_SelectRaceLabel))!;
        public static string MainWindow_ExportCustomAiButton => ResourceManager.GetString(nameof(MainWindow_ExportCustomAiButton))!;
        public static string MainWindow_ExportLiveriesButton => ResourceManager.GetString(nameof(MainWindow_ExportLiveriesButton))!;
        public static string MainWindow_BackButton => ResourceManager.GetString(nameof(MainWindow_BackButton))!;
        public static string MainWindow_DriverNameLabel => ResourceManager.GetString(nameof(MainWindow_DriverNameLabel))!;
        public static string MainWindow_NationalityLabel => ResourceManager.GetString(nameof(MainWindow_NationalityLabel))!;
        public static string MainWindow_AgeLabel => ResourceManager.GetString(nameof(MainWindow_AgeLabel))!;
        public static string MainWindow_FavouriteNumbersLabel => ResourceManager.GetString(nameof(MainWindow_FavouriteNumbersLabel))!;
        public static string MainWindow_FavouriteNumbersToolTip => ResourceManager.GetString(nameof(MainWindow_FavouriteNumbersToolTip))!;
        public static string MainWindow_ReputationLabel => ResourceManager.GetString(nameof(MainWindow_ReputationLabel))!;
        public static string MainWindow_HelmetDesignLabel => ResourceManager.GetString(nameof(MainWindow_HelmetDesignLabel))!;
        public static string MainWindow_CreateGameButton => ResourceManager.GetString(nameof(MainWindow_CreateGameButton))!;
        public static string MainWindow_SelectScenarioLabel => ResourceManager.GetString(nameof(MainWindow_SelectScenarioLabel))!;
        public static string MainWindow_StartScenarioButton => ResourceManager.GetString(nameof(MainWindow_StartScenarioButton))!;

        public static string MainWindow_GenericError_Title => ResourceManager.GetString(nameof(MainWindow_GenericError_Title))!;
        public static string MainWindow_ValidationError_Title => ResourceManager.GetString(nameof(MainWindow_ValidationError_Title))!;
        public static string MainWindow_DeveloperMode_Title => ResourceManager.GetString(nameof(MainWindow_DeveloperMode_Title))!;
        public static string MainWindow_Information_Title => ResourceManager.GetString(nameof(MainWindow_Information_Title))!;
        public static string MainWindow_ModOutOfDate_Title => ResourceManager.GetString(nameof(MainWindow_ModOutOfDate_Title))!;

        public static string MainWindow_NoSeasonsAvailable => ResourceManager.GetString(nameof(MainWindow_NoSeasonsAvailable))!;
        public static string MainWindow_ErrorLoadingSeasonsItem => ResourceManager.GetString(nameof(MainWindow_ErrorLoadingSeasonsItem))!;
        public static string MainWindow_LoadSeasonsError_Message => ResourceManager.GetString(nameof(MainWindow_LoadSeasonsError_Message))!;
        public static string MainWindow_CustomAiExported_Message => ResourceManager.GetString(nameof(MainWindow_CustomAiExported_Message))!;
        public static string MainWindow_ExportCustomAiError_Message => ResourceManager.GetString(nameof(MainWindow_ExportCustomAiError_Message))!;
        public static string MainWindow_LiveriesExported_Message => ResourceManager.GetString(nameof(MainWindow_LiveriesExported_Message))!;
        public static string MainWindow_ExportLiveriesError_Message => ResourceManager.GetString(nameof(MainWindow_ExportLiveriesError_Message))!;
        public static string MainWindow_SelectSeasonAndRaceFirst_Message => ResourceManager.GetString(nameof(MainWindow_SelectSeasonAndRaceFirst_Message))!;
        public static string MainWindow_LoadGame_DialogTitle => ResourceManager.GetString(nameof(MainWindow_LoadGame_DialogTitle))!;
        public static string MainWindow_ModOutOfDate_Message => ResourceManager.GetString(nameof(MainWindow_ModOutOfDate_Message))!;
        public static string MainWindow_LoadGameError_Message => ResourceManager.GetString(nameof(MainWindow_LoadGameError_Message))!;
        public static string MainWindow_RequiredFieldsMissing_Message => ResourceManager.GetString(nameof(MainWindow_RequiredFieldsMissing_Message))!;
        public static string MainWindow_SeasonNotInstalled_Message => ResourceManager.GetString(nameof(MainWindow_SeasonNotInstalled_Message))!;
        public static string MainWindow_PayDriverAgeInvalid_Message => ResourceManager.GetString(nameof(MainWindow_PayDriverAgeInvalid_Message))!;
        public static string MainWindow_CreateGameError_Message => ResourceManager.GetString(nameof(MainWindow_CreateGameError_Message))!;
        public static string MainWindow_LoadScenarioError_Message => ResourceManager.GetString(nameof(MainWindow_LoadScenarioError_Message))!;
        public static string MainWindow_NoScenariosAvailable_Message => ResourceManager.GetString(nameof(MainWindow_NoScenariosAvailable_Message))!;
        public static string MainWindow_SelectScenarioFirst_Message => ResourceManager.GetString(nameof(MainWindow_SelectScenarioFirst_Message))!;
        public static string MainWindow_ScenarioListItem_Format => ResourceManager.GetString(nameof(MainWindow_ScenarioListItem_Format))!;

        public static string MainWindow_Reputation_PayDriverWildCard_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PayDriverWildCard_Name))!;
        public static string MainWindow_Reputation_PayDriverWildCard_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PayDriverWildCard_Description))!;
        public static string MainWindow_Reputation_PayDriverSeason_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PayDriverSeason_Name))!;
        public static string MainWindow_Reputation_PayDriverSeason_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PayDriverSeason_Description))!;
        public static string MainWindow_Reputation_YoungTalent_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_YoungTalent_Name))!;
        public static string MainWindow_Reputation_YoungTalent_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_YoungTalent_Description))!;
        public static string MainWindow_Reputation_YoungChampionshipUnproven_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_YoungChampionshipUnproven_Name))!;
        public static string MainWindow_Reputation_YoungChampionshipUnproven_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_YoungChampionshipUnproven_Description))!;
        public static string MainWindow_Reputation_YoungChampionship_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_YoungChampionship_Name))!;
        public static string MainWindow_Reputation_YoungChampionship_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_YoungChampionship_Description))!;
        public static string MainWindow_Reputation_PrimeMidfield_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeMidfield_Name))!;
        public static string MainWindow_Reputation_PrimeMidfield_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeMidfield_Description))!;
        public static string MainWindow_Reputation_PrimeStrongMidfield_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeStrongMidfield_Name))!;
        public static string MainWindow_Reputation_PrimeStrongMidfield_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeStrongMidfield_Description))!;
        public static string MainWindow_Reputation_PrimeChampionshipUnproven_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeChampionshipUnproven_Name))!;
        public static string MainWindow_Reputation_PrimeChampionshipUnproven_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeChampionshipUnproven_Description))!;
        public static string MainWindow_Reputation_PrimeChampionship_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeChampionship_Name))!;
        public static string MainWindow_Reputation_PrimeChampionship_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeChampionship_Description))!;
        public static string MainWindow_Reputation_PrimeChampionshipWashed_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeChampionshipWashed_Name))!;
        public static string MainWindow_Reputation_PrimeChampionshipWashed_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_PrimeChampionshipWashed_Description))!;
        public static string MainWindow_Reputation_AgeingMidfield_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingMidfield_Name))!;
        public static string MainWindow_Reputation_AgeingMidfield_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingMidfield_Description))!;
        public static string MainWindow_Reputation_AgeingStrongMidfield_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingStrongMidfield_Name))!;
        public static string MainWindow_Reputation_AgeingStrongMidfield_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingStrongMidfield_Description))!;
        public static string MainWindow_Reputation_AgeingChampionship_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingChampionship_Name))!;
        public static string MainWindow_Reputation_AgeingChampionship_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingChampionship_Description))!;
        public static string MainWindow_Reputation_AgeingChampionshipWashed_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingChampionshipWashed_Name))!;
        public static string MainWindow_Reputation_AgeingChampionshipWashed_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_AgeingChampionshipWashed_Description))!;
        public static string MainWindow_Reputation_JustOneLastDance_Name => ResourceManager.GetString(nameof(MainWindow_Reputation_JustOneLastDance_Name))!;
        public static string MainWindow_Reputation_JustOneLastDance_Description => ResourceManager.GetString(nameof(MainWindow_Reputation_JustOneLastDance_Description))!;

        public static string DriverStandingsGridWindow_Title => ResourceManager.GetString(nameof(DriverStandingsGridWindow_Title))!;
        public static string DriverStandingsGridWindow_Header => ResourceManager.GetString(nameof(DriverStandingsGridWindow_Header))!;
        public static string DriverStandingsGridWindow_SeasonYear_Format => ResourceManager.GetString(nameof(DriverStandingsGridWindow_SeasonYear_Format))!;
        public static string DriverStandingsGridWindow_FooterText => ResourceManager.GetString(nameof(DriverStandingsGridWindow_FooterText))!;
        public static string DriverStandingsGridWindow_DriverColumnHeader => ResourceManager.GetString(nameof(DriverStandingsGridWindow_DriverColumnHeader))!;

        public static string ConstructorStandingsGridWindow_Title => ResourceManager.GetString(nameof(ConstructorStandingsGridWindow_Title))!;
        public static string ConstructorStandingsGridWindow_Header => ResourceManager.GetString(nameof(ConstructorStandingsGridWindow_Header))!;
        public static string ConstructorStandingsGridWindow_SeasonYear_Format => ResourceManager.GetString(nameof(ConstructorStandingsGridWindow_SeasonYear_Format))!;
        public static string ConstructorStandingsGridWindow_FooterText => ResourceManager.GetString(nameof(ConstructorStandingsGridWindow_FooterText))!;
        public static string ConstructorStandingsGridWindow_TeamColumnHeader => ResourceManager.GetString(nameof(ConstructorStandingsGridWindow_TeamColumnHeader))!;

        public static string HistoricalStandingsWindow_Title => ResourceManager.GetString(nameof(HistoricalStandingsWindow_Title))!;
        public static string HistoricalStandingsWindow_Header => ResourceManager.GetString(nameof(HistoricalStandingsWindow_Header))!;
        public static string HistoricalStandingsWindow_SelectSeasonLabel => ResourceManager.GetString(nameof(HistoricalStandingsWindow_SelectSeasonLabel))!;
        public static string HistoricalStandingsWindow_DriversChampionshipHeader => ResourceManager.GetString(nameof(HistoricalStandingsWindow_DriversChampionshipHeader))!;
        public static string HistoricalStandingsWindow_ConstructorsChampionshipHeader => ResourceManager.GetString(nameof(HistoricalStandingsWindow_ConstructorsChampionshipHeader))!;
        public static string HistoricalStandingsWindow_NoDataMessage => ResourceManager.GetString(nameof(HistoricalStandingsWindow_NoDataMessage))!;

        public static string MissingDriversResultWindow_Title => ResourceManager.GetString(nameof(MissingDriversResultWindow_Title))!;
        public static string MissingDriversResultWindow_Header => ResourceManager.GetString(nameof(MissingDriversResultWindow_Header))!;
        public static string MissingDriversResultWindow_Description => ResourceManager.GetString(nameof(MissingDriversResultWindow_Description))!;
        public static string MissingDriversResultWindow_MissingQualifyingHeader => ResourceManager.GetString(nameof(MissingDriversResultWindow_MissingQualifyingHeader))!;
        public static string MissingDriversResultWindow_MissingRaceHeader => ResourceManager.GetString(nameof(MissingDriversResultWindow_MissingRaceHeader))!;
        public static string MissingDriversResultWindow_DriverColumnHeader => ResourceManager.GetString(nameof(MissingDriversResultWindow_DriverColumnHeader))!;
        public static string MissingDriversResultWindow_FastestLapColumnHeader => ResourceManager.GetString(nameof(MissingDriversResultWindow_FastestLapColumnHeader))!;
        public static string MissingDriversResultWindow_ConfirmButton => ResourceManager.GetString(nameof(MissingDriversResultWindow_ConfirmButton))!;
        public static string MissingDriversResultWindow_PlayerSuffix => ResourceManager.GetString(nameof(MissingDriversResultWindow_PlayerSuffix))!;

        public static string PreQualiResultsWindow_Title => ResourceManager.GetString(nameof(PreQualiResultsWindow_Title))!;
        public static string PreQualiResultsWindow_SessionBanner => ResourceManager.GetString(nameof(PreQualiResultsWindow_SessionBanner))!;
        public static string PreQualiResultsWindow_OfficialResultsHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_OfficialResultsHeader))!;
        public static string PreQualiResultsWindow_PositionColumnHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_PositionColumnHeader))!;
        public static string PreQualiResultsWindow_NumberColumnHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_NumberColumnHeader))!;
        public static string PreQualiResultsWindow_DriverColumnHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_DriverColumnHeader))!;
        public static string PreQualiResultsWindow_TeamColumnHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_TeamColumnHeader))!;
        public static string PreQualiResultsWindow_BestLapColumnHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_BestLapColumnHeader))!;
        public static string PreQualiResultsWindow_StatusColumnHeader => ResourceManager.GetString(nameof(PreQualiResultsWindow_StatusColumnHeader))!;
        public static string PreQualiResultsWindow_DidNotPreQualifyBanner => ResourceManager.GetString(nameof(PreQualiResultsWindow_DidNotPreQualifyBanner))!;
        public static string PreQualiResultsWindow_ContinueButton => ResourceManager.GetString(nameof(PreQualiResultsWindow_ContinueButton))!;
        public static string PreQualiResultsWindow_SessionSummary_Singular => ResourceManager.GetString(nameof(PreQualiResultsWindow_SessionSummary_Singular))!;
        public static string PreQualiResultsWindow_SessionSummary_Plural => ResourceManager.GetString(nameof(PreQualiResultsWindow_SessionSummary_Plural))!;
        public static string PreQualiResultsWindow_Footer_WithYear => ResourceManager.GetString(nameof(PreQualiResultsWindow_Footer_WithYear))!;
        public static string PreQualiResultsWindow_Footer_NoYear => ResourceManager.GetString(nameof(PreQualiResultsWindow_Footer_NoYear))!;
        public static string PreQualiResultsWindow_StatusQualified => ResourceManager.GetString(nameof(PreQualiResultsWindow_StatusQualified))!;
        public static string PreQualiResultsWindow_StatusDnpq => ResourceManager.GetString(nameof(PreQualiResultsWindow_StatusDnpq))!;

        public static string SeasonOverviewWindow_Title => ResourceManager.GetString(nameof(SeasonOverviewWindow_Title))!;
        public static string SeasonOverviewWindow_HistoricalStandingsTooltip => ResourceManager.GetString(nameof(SeasonOverviewWindow_HistoricalStandingsTooltip))!;
        public static string SeasonOverviewWindow_PlayerHeader => ResourceManager.GetString(nameof(SeasonOverviewWindow_PlayerHeader))!;
        public static string SeasonOverviewWindow_NameLabel => ResourceManager.GetString(nameof(SeasonOverviewWindow_NameLabel))!;
        public static string SeasonOverviewWindow_TeamLabel => ResourceManager.GetString(nameof(SeasonOverviewWindow_TeamLabel))!;
        public static string SeasonOverviewWindow_ReputationLabel => ResourceManager.GetString(nameof(SeasonOverviewWindow_ReputationLabel))!;
        public static string SeasonOverviewWindow_EditDetailsButton => ResourceManager.GetString(nameof(SeasonOverviewWindow_EditDetailsButton))!;
        public static string SeasonOverviewWindow_NextGrandPrixLabel => ResourceManager.GetString(nameof(SeasonOverviewWindow_NextGrandPrixLabel))!;
        public static string SeasonOverviewWindow_ProceedButton => ResourceManager.GetString(nameof(SeasonOverviewWindow_ProceedButton))!;
        public static string SeasonOverviewWindow_GridViewTooltip => ResourceManager.GetString(nameof(SeasonOverviewWindow_GridViewTooltip))!;
        public static string SeasonOverviewWindow_DriverStandingsHeader => ResourceManager.GetString(nameof(SeasonOverviewWindow_DriverStandingsHeader))!;
        public static string SeasonOverviewWindow_ConstructorStandingsHeader => ResourceManager.GetString(nameof(SeasonOverviewWindow_ConstructorStandingsHeader))!;
        public static string SeasonOverviewWindow_SeasonText_Format => ResourceManager.GetString(nameof(SeasonOverviewWindow_SeasonText_Format))!;
        public static string SeasonOverviewWindow_RoundInfo_Format => ResourceManager.GetString(nameof(SeasonOverviewWindow_RoundInfo_Format))!;
        public static string SeasonOverviewWindow_SeasonComplete => ResourceManager.GetString(nameof(SeasonOverviewWindow_SeasonComplete))!;
        public static string SeasonOverviewWindow_NoTeam => ResourceManager.GetString(nameof(SeasonOverviewWindow_NoTeam))!;
        public static string SeasonOverviewWindow_UnknownReputation => ResourceManager.GetString(nameof(SeasonOverviewWindow_UnknownReputation))!;
        public static string SeasonOverviewWindow_UnknownDriver => ResourceManager.GetString(nameof(SeasonOverviewWindow_UnknownDriver))!;
        public static string SeasonOverviewWindow_UnknownTeam => ResourceManager.GetString(nameof(SeasonOverviewWindow_UnknownTeam))!;
        public static string SeasonOverviewWindow_GrandPrixFallback => ResourceManager.GetString(nameof(SeasonOverviewWindow_GrandPrixFallback))!;
        public static string SeasonOverviewWindow_PrepareGpError_Message => ResourceManager.GetString(nameof(SeasonOverviewWindow_PrepareGpError_Message))!;
        public static string SeasonOverviewWindow_OffSeasonError_Message => ResourceManager.GetString(nameof(SeasonOverviewWindow_OffSeasonError_Message))!;
        public static string SeasonOverviewWindow_SeasonUnavailable_Message => ResourceManager.GetString(nameof(SeasonOverviewWindow_SeasonUnavailable_Message))!;
        public static string SeasonOverviewWindow_GenericError_Title => ResourceManager.GetString(nameof(SeasonOverviewWindow_GenericError_Title))!;

        public static string DriverAccoladesWindow_Title => ResourceManager.GetString(nameof(DriverAccoladesWindow_Title))!;
        public static string DriverAccoladesWindow_WinsLabel => ResourceManager.GetString(nameof(DriverAccoladesWindow_WinsLabel))!;
        public static string DriverAccoladesWindow_PodiumsLabel => ResourceManager.GetString(nameof(DriverAccoladesWindow_PodiumsLabel))!;
        public static string DriverAccoladesWindow_PolesLabel => ResourceManager.GetString(nameof(DriverAccoladesWindow_PolesLabel))!;
        public static string DriverAccoladesWindow_ChampionshipsLabel => ResourceManager.GetString(nameof(DriverAccoladesWindow_ChampionshipsLabel))!;
        public static string DriverAccoladesWindow_PreviousSeasonsLabel => ResourceManager.GetString(nameof(DriverAccoladesWindow_PreviousSeasonsLabel))!;
        public static string DriverAccoladesWindow_YearColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_YearColumnHeader))!;
        public static string DriverAccoladesWindow_PositionColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_PositionColumnHeader))!;
        public static string DriverAccoladesWindow_TeamColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_TeamColumnHeader))!;
        public static string DriverAccoladesWindow_RacesColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_RacesColumnHeader))!;
        public static string DriverAccoladesWindow_WinsColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_WinsColumnHeader))!;
        public static string DriverAccoladesWindow_PodiumsColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_PodiumsColumnHeader))!;
        public static string DriverAccoladesWindow_PolesColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_PolesColumnHeader))!;
        public static string DriverAccoladesWindow_PointsColumnHeader => ResourceManager.GetString(nameof(DriverAccoladesWindow_PointsColumnHeader))!;
        public static string DriverAccoladesWindow_AgeText_Format => ResourceManager.GetString(nameof(DriverAccoladesWindow_AgeText_Format))!;

        public static string ConstructorAccoladesWindow_Title => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_Title))!;
        public static string ConstructorAccoladesWindow_WinsLabel => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_WinsLabel))!;
        public static string ConstructorAccoladesWindow_PodiumsLabel => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PodiumsLabel))!;
        public static string ConstructorAccoladesWindow_PolesLabel => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PolesLabel))!;
        public static string ConstructorAccoladesWindow_ChampionshipsLabel => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_ChampionshipsLabel))!;
        public static string ConstructorAccoladesWindow_PreviousSeasonsLabel => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PreviousSeasonsLabel))!;
        public static string ConstructorAccoladesWindow_YearColumnHeader => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_YearColumnHeader))!;
        public static string ConstructorAccoladesWindow_PositionColumnHeader => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PositionColumnHeader))!;
        public static string ConstructorAccoladesWindow_PointsColumnHeader => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PointsColumnHeader))!;
        public static string ConstructorAccoladesWindow_WinsColumnHeader => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_WinsColumnHeader))!;
        public static string ConstructorAccoladesWindow_PodiumsColumnHeader => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PodiumsColumnHeader))!;
        public static string ConstructorAccoladesWindow_PolesColumnHeader => ResourceManager.GetString(nameof(ConstructorAccoladesWindow_PolesColumnHeader))!;

        public static string RaceInstructionsWindow_Title => ResourceManager.GetString(nameof(RaceInstructionsWindow_Title))!;
        public static string RaceInstructionsWindow_ReadyHeader => ResourceManager.GetString(nameof(RaceInstructionsWindow_ReadyHeader))!;
        public static string RaceInstructionsWindow_AutoSaveNote => ResourceManager.GetString(nameof(RaceInstructionsWindow_AutoSaveNote))!;
        public static string RaceInstructionsWindow_OkButton => ResourceManager.GetString(nameof(RaceInstructionsWindow_OkButton))!;
        public static string RaceInstructionsWindow_Intro1_Format => ResourceManager.GetString(nameof(RaceInstructionsWindow_Intro1_Format))!;
        public static string RaceInstructionsWindow_Intro2_Format => ResourceManager.GetString(nameof(RaceInstructionsWindow_Intro2_Format))!;
        public static string RaceInstructionsWindow_Intro3_UsesScalars => ResourceManager.GetString(nameof(RaceInstructionsWindow_Intro3_UsesScalars))!;
        public static string RaceInstructionsWindow_Intro3_Format => ResourceManager.GetString(nameof(RaceInstructionsWindow_Intro3_Format))!;
        public static string RaceInstructionsWindow_Intro4 => ResourceManager.GetString(nameof(RaceInstructionsWindow_Intro4))!;
        public static string RaceInstructionsWindow_PreQuali_Title => ResourceManager.GetString(nameof(RaceInstructionsWindow_PreQuali_Title))!;
        public static string RaceInstructionsWindow_PreQuali_Intro4 => ResourceManager.GetString(nameof(RaceInstructionsWindow_PreQuali_Intro4))!;
        public static string RaceInstructionsWindow_PreQuali_OkButton => ResourceManager.GetString(nameof(RaceInstructionsWindow_PreQuali_OkButton))!;

        public static string RaceCalendarSelectionWindow_Title => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_Title))!;
        public static string RaceCalendarSelectionWindow_Header => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_Header))!;
        public static string RaceCalendarSelectionWindow_Subtitle => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_Subtitle))!;
        public static string RaceCalendarSelectionWindow_RacesSelectedLabel => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_RacesSelectedLabel))!;
        public static string RaceCalendarSelectionWindow_WarningText => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_WarningText))!;
        public static string RaceCalendarSelectionWindow_RoundColumnHeader => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_RoundColumnHeader))!;
        public static string RaceCalendarSelectionWindow_GrandPrixColumnHeader => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_GrandPrixColumnHeader))!;
        public static string RaceCalendarSelectionWindow_CircuitColumnHeader => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_CircuitColumnHeader))!;
        public static string RaceCalendarSelectionWindow_DateColumnHeader => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_DateColumnHeader))!;
        public static string RaceCalendarSelectionWindow_IncludeColumnHeader => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_IncludeColumnHeader))!;
        public static string RaceCalendarSelectionWindow_ConfirmButton => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_ConfirmButton))!;
        public static string RaceCalendarSelectionWindow_SeasonText_Format => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_SeasonText_Format))!;
        public static string RaceCalendarSelectionWindow_InvalidSelection_Message => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_InvalidSelection_Message))!;
        public static string RaceCalendarSelectionWindow_InvalidSelection_Title => ResourceManager.GetString(nameof(RaceCalendarSelectionWindow_InvalidSelection_Title))!;

        public static string EntryListWindow_Title => ResourceManager.GetString(nameof(EntryListWindow_Title))!;
        public static string EntryListWindow_PreQualiBanner => ResourceManager.GetString(nameof(EntryListWindow_PreQualiBanner))!;
        public static string EntryListWindow_NumberColumnHeader => ResourceManager.GetString(nameof(EntryListWindow_NumberColumnHeader))!;
        public static string EntryListWindow_DriverColumnHeader => ResourceManager.GetString(nameof(EntryListWindow_DriverColumnHeader))!;
        public static string EntryListWindow_NationalityColumnHeader => ResourceManager.GetString(nameof(EntryListWindow_NationalityColumnHeader))!;
        public static string EntryListWindow_TeamColumnHeader => ResourceManager.GetString(nameof(EntryListWindow_TeamColumnHeader))!;
        public static string EntryListWindow_OfficialTitle => ResourceManager.GetString(nameof(EntryListWindow_OfficialTitle))!;
        public static string EntryListWindow_PreQualiTitle => ResourceManager.GetString(nameof(EntryListWindow_PreQualiTitle))!;
        public static string EntryListWindow_OfficialContinueButton => ResourceManager.GetString(nameof(EntryListWindow_OfficialContinueButton))!;
        public static string EntryListWindow_PreQualiContinueButton => ResourceManager.GetString(nameof(EntryListWindow_PreQualiContinueButton))!;
        public static string EntryListWindow_PreQualiFooter_Format => ResourceManager.GetString(nameof(EntryListWindow_PreQualiFooter_Format))!;
        public static string EntryListWindow_OfficialFooter_Format => ResourceManager.GetString(nameof(EntryListWindow_OfficialFooter_Format))!;
        public static string EntryListWindow_UnknownTeam => ResourceManager.GetString(nameof(EntryListWindow_UnknownTeam))!;
        public static string EntryListWindow_ApplyingLiveries_Message => ResourceManager.GetString(nameof(EntryListWindow_ApplyingLiveries_Message))!;
        public static string EntryListWindow_ApplyingPreQualiLiveries_Message => ResourceManager.GetString(nameof(EntryListWindow_ApplyingPreQualiLiveries_Message))!;
        public static string EntryListWindow_ApplyLiveriesError_Message => ResourceManager.GetString(nameof(EntryListWindow_ApplyLiveriesError_Message))!;
        public static string EntryListWindow_GenericError_Title => ResourceManager.GetString(nameof(EntryListWindow_GenericError_Title))!;
        public static string EntryListWindow_AddDriverName_Message => ResourceManager.GetString(nameof(EntryListWindow_AddDriverName_Message))!;
        public static string EntryListWindow_AddDriverName_Title => ResourceManager.GetString(nameof(EntryListWindow_AddDriverName_Title))!;
        public static string EntryListWindow_DidNotPreQualify_Message => ResourceManager.GetString(nameof(EntryListWindow_DidNotPreQualify_Message))!;
        public static string EntryListWindow_DidNotPreQualify_Title => ResourceManager.GetString(nameof(EntryListWindow_DidNotPreQualify_Title))!;

        public static string RaceWeekendWindow_Title => ResourceManager.GetString(nameof(RaceWeekendWindow_Title))!;
        public static string RaceWeekendWindow_WaitingMessage => ResourceManager.GetString(nameof(RaceWeekendWindow_WaitingMessage))!;
        public static string RaceWeekendWindow_PositionColumnHeader => ResourceManager.GetString(nameof(RaceWeekendWindow_PositionColumnHeader))!;
        public static string RaceWeekendWindow_NumberColumnHeader => ResourceManager.GetString(nameof(RaceWeekendWindow_NumberColumnHeader))!;
        public static string RaceWeekendWindow_NameColumnHeader => ResourceManager.GetString(nameof(RaceWeekendWindow_NameColumnHeader))!;
        public static string RaceWeekendWindow_NationalityColumnHeader => ResourceManager.GetString(nameof(RaceWeekendWindow_NationalityColumnHeader))!;
        public static string RaceWeekendWindow_TeamColumnHeader => ResourceManager.GetString(nameof(RaceWeekendWindow_TeamColumnHeader))!;
        public static string RaceWeekendWindow_BestLapColumnHeader => ResourceManager.GetString(nameof(RaceWeekendWindow_BestLapColumnHeader))!;
        public static string RaceWeekendWindow_EndRaceButton => ResourceManager.GetString(nameof(RaceWeekendWindow_EndRaceButton))!;
        public static string RaceWeekendWindow_PreQualiSessionLabel => ResourceManager.GetString(nameof(RaceWeekendWindow_PreQualiSessionLabel))!;
        public static string RaceWeekendWindow_PreQualiSessionName => ResourceManager.GetString(nameof(RaceWeekendWindow_PreQualiSessionName))!;
        public static string RaceWeekendWindow_SessionLabel_Modern => ResourceManager.GetString(nameof(RaceWeekendWindow_SessionLabel_Modern))!;
        public static string RaceWeekendWindow_SessionLabel_Classic => ResourceManager.GetString(nameof(RaceWeekendWindow_SessionLabel_Classic))!;
        public static string RaceWeekendWindow_Session_Practice_Modern => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Practice_Modern))!;
        public static string RaceWeekendWindow_Session_Qualification_Modern => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Qualification_Modern))!;
        public static string RaceWeekendWindow_Session_Race_Modern => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Race_Modern))!;
        public static string RaceWeekendWindow_Session_Unknown_Modern => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Unknown_Modern))!;
        public static string RaceWeekendWindow_Session_Practice_Classic => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Practice_Classic))!;
        public static string RaceWeekendWindow_Session_Qualification_Classic => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Qualification_Classic))!;
        public static string RaceWeekendWindow_Session_Race_Classic => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Race_Classic))!;
        public static string RaceWeekendWindow_Session_Unknown_Classic => ResourceManager.GetString(nameof(RaceWeekendWindow_Session_Unknown_Classic))!;
        public static string RaceWeekendWindow_FinishSessionFirst_Message => ResourceManager.GetString(nameof(RaceWeekendWindow_FinishSessionFirst_Message))!;
        public static string RaceWeekendWindow_Info_Title => ResourceManager.GetString(nameof(RaceWeekendWindow_Info_Title))!;
        public static string RaceWeekendWindow_UseControlWindow_Message => ResourceManager.GetString(nameof(RaceWeekendWindow_UseControlWindow_Message))!;
        public static string RaceWeekendWindow_DefaultCircuitName => ResourceManager.GetString(nameof(RaceWeekendWindow_DefaultCircuitName))!;

        public static string GenerateAbsenceWindow_WindowTitle => ResourceManager.GetString(nameof(GenerateAbsenceWindow_WindowTitle))!;
        public static string GenerateAbsenceWindow_PayDriverTitle => ResourceManager.GetString(nameof(GenerateAbsenceWindow_PayDriverTitle))!;
        public static string GenerateAbsenceWindow_NoTeamSeasonTitle => ResourceManager.GetString(nameof(GenerateAbsenceWindow_NoTeamSeasonTitle))!;
        public static string GenerateAbsenceWindow_NoTeamRaceTitle => ResourceManager.GetString(nameof(GenerateAbsenceWindow_NoTeamRaceTitle))!;
        public static string GenerateAbsenceWindow_PayDriver_Intro1 => ResourceManager.GetString(nameof(GenerateAbsenceWindow_PayDriver_Intro1))!;
        public static string GenerateAbsenceWindow_PayDriver_Intro2 => ResourceManager.GetString(nameof(GenerateAbsenceWindow_PayDriver_Intro2))!;
        public static string GenerateAbsenceWindow_Intro3 => ResourceManager.GetString(nameof(GenerateAbsenceWindow_Intro3))!;
        public static string GenerateAbsenceWindow_QuestionText => ResourceManager.GetString(nameof(GenerateAbsenceWindow_QuestionText))!;
        public static string GenerateAbsenceWindow_FooterNote => ResourceManager.GetString(nameof(GenerateAbsenceWindow_FooterNote))!;
        public static string GenerateAbsenceWindow_YesButton => ResourceManager.GetString(nameof(GenerateAbsenceWindow_YesButton))!;
        public static string GenerateAbsenceWindow_NoButton => ResourceManager.GetString(nameof(GenerateAbsenceWindow_NoButton))!;
        public static string GenerateAbsenceWindow_BackButton => ResourceManager.GetString(nameof(GenerateAbsenceWindow_BackButton))!;
        public static string GenerateAbsenceWindow_NoTeamSeason_Intro1 => ResourceManager.GetString(nameof(GenerateAbsenceWindow_NoTeamSeason_Intro1))!;
        public static string GenerateAbsenceWindow_SeekOpportunity_Intro2 => ResourceManager.GetString(nameof(GenerateAbsenceWindow_SeekOpportunity_Intro2))!;
        public static string GenerateAbsenceWindow_NoTeamRace_Intro1 => ResourceManager.GetString(nameof(GenerateAbsenceWindow_NoTeamRace_Intro1))!;

        public static string TeamSelectionWindow_Title => ResourceManager.GetString(nameof(TeamSelectionWindow_Title))!;
        public static string TeamSelectionWindow_Header => ResourceManager.GetString(nameof(TeamSelectionWindow_Header))!;
        public static string TeamSelectionWindow_FreeAgentsHeader => ResourceManager.GetString(nameof(TeamSelectionWindow_FreeAgentsHeader))!;
        public static string TeamSelectionWindow_ConfirmButton => ResourceManager.GetString(nameof(TeamSelectionWindow_ConfirmButton))!;
        public static string TeamSelectionWindow_BackButton => ResourceManager.GetString(nameof(TeamSelectionWindow_BackButton))!;
        public static string TeamSelectionWindow_FirstDriverRole => ResourceManager.GetString(nameof(TeamSelectionWindow_FirstDriverRole))!;
        public static string TeamSelectionWindow_SecondDriverRole => ResourceManager.GetString(nameof(TeamSelectionWindow_SecondDriverRole))!;
        public static string TeamSelectionWindow_FreeAgentRole => ResourceManager.GetString(nameof(TeamSelectionWindow_FreeAgentRole))!;
        public static string TeamSelectionWindow_LoadError_Message => ResourceManager.GetString(nameof(TeamSelectionWindow_LoadError_Message))!;
        public static string TeamSelectionWindow_LoadError_Title => ResourceManager.GetString(nameof(TeamSelectionWindow_LoadError_Title))!;
        public static string TeamSelectionWindow_NoDriverSelected_Message => ResourceManager.GetString(nameof(TeamSelectionWindow_NoDriverSelected_Message))!;
        public static string TeamSelectionWindow_NoDriverSelected_Title => ResourceManager.GetString(nameof(TeamSelectionWindow_NoDriverSelected_Title))!;

        public static string TeamApplicationWindow_Title => ResourceManager.GetString(nameof(TeamApplicationWindow_Title))!;
        public static string TeamApplicationWindow_Header => ResourceManager.GetString(nameof(TeamApplicationWindow_Header))!;
        public static string TeamApplicationWindow_Subtitle => ResourceManager.GetString(nameof(TeamApplicationWindow_Subtitle))!;
        public static string TeamApplicationWindow_InstructionText => ResourceManager.GetString(nameof(TeamApplicationWindow_InstructionText))!;
        public static string TeamApplicationWindow_ContinueButton => ResourceManager.GetString(nameof(TeamApplicationWindow_ContinueButton))!;
        public static string TeamApplicationWindow_UnknownTeam => ResourceManager.GetString(nameof(TeamApplicationWindow_UnknownTeam))!;
        public static string TeamApplicationWindow_FirstDriverRole => ResourceManager.GetString(nameof(TeamApplicationWindow_FirstDriverRole))!;
        public static string TeamApplicationWindow_SecondDriverRole => ResourceManager.GetString(nameof(TeamApplicationWindow_SecondDriverRole))!;
        public static string TeamApplicationWindow_AlreadyInterestedStatus => ResourceManager.GetString(nameof(TeamApplicationWindow_AlreadyInterestedStatus))!;
        public static string TeamApplicationWindow_ClickToApplyStatus => ResourceManager.GetString(nameof(TeamApplicationWindow_ClickToApplyStatus))!;
        public static string TeamApplicationWindow_MaxApplications_Message => ResourceManager.GetString(nameof(TeamApplicationWindow_MaxApplications_Message))!;
        public static string TeamApplicationWindow_MaxApplications_Title => ResourceManager.GetString(nameof(TeamApplicationWindow_MaxApplications_Title))!;
        public static string TeamApplicationWindow_SelectionCount_Singular => ResourceManager.GetString(nameof(TeamApplicationWindow_SelectionCount_Singular))!;
        public static string TeamApplicationWindow_SelectionCount_Plural => ResourceManager.GetString(nameof(TeamApplicationWindow_SelectionCount_Plural))!;
        public static string TeamApplicationWindow_DropReason_ContractExpired => ResourceManager.GetString(nameof(TeamApplicationWindow_DropReason_ContractExpired))!;
        public static string TeamApplicationWindow_DropReason_Underperforming => ResourceManager.GetString(nameof(TeamApplicationWindow_DropReason_Underperforming))!;
        public static string TeamApplicationWindow_DropReason_Retiring => ResourceManager.GetString(nameof(TeamApplicationWindow_DropReason_Retiring))!;
        public static string TeamApplicationWindow_DropReason_TeamQuitting => ResourceManager.GetString(nameof(TeamApplicationWindow_DropReason_TeamQuitting))!;
        public static string TeamApplicationWindow_DropReason_PlayerRejecting => ResourceManager.GetString(nameof(TeamApplicationWindow_DropReason_PlayerRejecting))!;

        public static string SeasonCatalogDialog_Title => ResourceManager.GetString(nameof(SeasonCatalogDialog_Title))!;
        public static string SeasonCatalogDialog_Header => ResourceManager.GetString(nameof(SeasonCatalogDialog_Header))!;
        public static string SeasonCatalogDialog_Description => ResourceManager.GetString(nameof(SeasonCatalogDialog_Description))!;
        public static string SeasonCatalogDialog_SeasonColumnHeader => ResourceManager.GetString(nameof(SeasonCatalogDialog_SeasonColumnHeader))!;
        public static string SeasonCatalogDialog_StatusColumnHeader => ResourceManager.GetString(nameof(SeasonCatalogDialog_StatusColumnHeader))!;
        public static string SeasonCatalogDialog_SizeColumnHeader => ResourceManager.GetString(nameof(SeasonCatalogDialog_SizeColumnHeader))!;
        public static string SeasonCatalogDialog_CloseButton => ResourceManager.GetString(nameof(SeasonCatalogDialog_CloseButton))!;
        public static string SeasonCatalogDialog_DownloadButton => ResourceManager.GetString(nameof(SeasonCatalogDialog_DownloadButton))!;
        public static string SeasonCatalogDialog_DownloadPrompt_Message => ResourceManager.GetString(nameof(SeasonCatalogDialog_DownloadPrompt_Message))!;
        public static string SeasonCatalogDialog_DownloadPrompt_Title => ResourceManager.GetString(nameof(SeasonCatalogDialog_DownloadPrompt_Title))!;
        public static string SeasonCatalogDialog_LocateFile_Title => ResourceManager.GetString(nameof(SeasonCatalogDialog_LocateFile_Title))!;
        public static string SeasonCatalogDialog_FileFilter_Format => ResourceManager.GetString(nameof(SeasonCatalogDialog_FileFilter_Format))!;
        public static string SeasonCatalogDialog_Installing_Format => ResourceManager.GetString(nameof(SeasonCatalogDialog_Installing_Format))!;
        public static string SeasonCatalogDialog_InstallError_Message => ResourceManager.GetString(nameof(SeasonCatalogDialog_InstallError_Message))!;
        public static string SeasonCatalogDialog_InstallError_Title => ResourceManager.GetString(nameof(SeasonCatalogDialog_InstallError_Title))!;

        public static string UpdateAvailableDialog_Title => ResourceManager.GetString(nameof(UpdateAvailableDialog_Title))!;
        public static string UpdateAvailableDialog_Header => ResourceManager.GetString(nameof(UpdateAvailableDialog_Header))!;
        public static string UpdateAvailableDialog_CurrentLabel => ResourceManager.GetString(nameof(UpdateAvailableDialog_CurrentLabel))!;
        public static string UpdateAvailableDialog_NewVersionLabel => ResourceManager.GetString(nameof(UpdateAvailableDialog_NewVersionLabel))!;
        public static string UpdateAvailableDialog_Step1Description => ResourceManager.GetString(nameof(UpdateAvailableDialog_Step1Description))!;
        public static string UpdateAvailableDialog_NotNowButton => ResourceManager.GetString(nameof(UpdateAvailableDialog_NotNowButton))!;
        public static string UpdateAvailableDialog_GoToDownloadPageButton => ResourceManager.GetString(nameof(UpdateAvailableDialog_GoToDownloadPageButton))!;
        public static string UpdateAvailableDialog_Step2Description => ResourceManager.GetString(nameof(UpdateAvailableDialog_Step2Description))!;
        public static string UpdateAvailableDialog_DownloadedItButton => ResourceManager.GetString(nameof(UpdateAvailableDialog_DownloadedItButton))!;
        public static string UpdateAvailableDialog_LocateFileTitle => ResourceManager.GetString(nameof(UpdateAvailableDialog_LocateFileTitle))!;
        public static string UpdateAvailableDialog_FileFilter => ResourceManager.GetString(nameof(UpdateAvailableDialog_FileFilter))!;
    }
}
