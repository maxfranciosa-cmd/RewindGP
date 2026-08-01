# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

"Rewind GP" (assembly/repo name `AMS2ChEd` — AMS2 Championship Edition) is a Windows WPF career-mode manager for **Automobilista 2** (AMS2). It lets a player create a driver, get hired/replace a driver on a historical grid (e.g. 1996, 1997 F1 seasons), play through a season race-by-race against AMS2's AI, and have the app handle everything the game itself doesn't: contract negotiations, driver reputation, end-of-season driver movements, standings, absences, and generating the liveries/AI roster files AMS2 needs for each race weekend. It reads live session results out of AMS2 via shared memory while a race weekend is in progress.

The codebase is mid-refactor (`decoupling` branch) towards a game-agnostic core with AMS2 as one pluggable "game module" — see [The game-module seam](#the-game-module-seam) below. AMS2 is still the only implemented game; the seam exists but nothing outside AMS2 is wired up yet.

Season content (drivers, teams, liveries, helmets, calendars) ships as data-driven "season packs" under `AMS2ChEd/Seasons/<year>/`, distributed/updated separately from app code (see `seasons_manifest.json` and the Updater section below) and authored with the companion `AMS2ChEd.SeasonPackCreator` tool.

## Solution layout

Open `AMS2ChEd/AMS2ChEd.sln`. Six projects, all targeting `net8.0` / `net8.0-windows`:

- **AMS2ChEd** — the main WPF app (`OutputType=WinExe`). MVVM-ish: `Views/` (XAML windows) + `Views/*.xaml.cs` code-behind drive most logic directly; `ViewModels/` and `Commands/` (a basic `RelayCommand`) are used for newer/simpler windows. DI is wired by hand in `App.xaml.cs::ConfigureServices` using `Microsoft.Extensions.DependencyInjection` — there is no auto-registration, so a new service/window must be registered there to be resolved via constructor injection. `ConfigureServices` registers game-agnostic services directly, then constructs a single `IGameModule` (currently `Ams2GameModule`) and delegates all game-specific registration to it — see [The game-module seam](#the-game-module-seam).
- **AMS2ChEd.Business** — game-agnostic domain layer. Defines interfaces (`GameLogic/Contracts`, `Services/Contracts`, `Storage/Contracts`) and base/default implementations (`GameLogic/Concrete`, `Services/`) for career-mode logic: contracts, reputation, standings, end-of-season driver movement, race-number allocation, the season-pack update pipeline (`Updater/`).
- **Ams2ChEd.Business.AMS2** — AMS2-specific layer. Subclasses/implements the Business contracts for AMS2 (`Ams2GameEngine`, `Ams2RacePreparator`, `Ams2RaceDataService`, etc.), and owns everything AMS2-file-format specific: JSON storage (`Storage/Concrete/JsonStorage`), livery/DDS generation (`Services/Ams2LiveryService.cs`, `Helpers/DdsTextureComposer.cs`), helmet picking, and the AMS2 shared-memory telemetry reader. Carries `ExternalDependencies/AMS2SharedMemoryNet.dll`, a third-party binary (no source) for reading AMS2's `$pcars2$` shared-memory block — referenced as a plain `<Reference>`, not a NuGet package.
- **AMS2ChEd.SeasonPackCreator** — separate WPF tool for authoring new season packs: driver/team/race/livery editors, a 3D helmet editor (HelixToolkit), and a `JolpicaF1Service` that pulls real historical F1 data from the Jolpica API to bootstrap season data. Not needed to work on the main app.
- **AMS2ChEd.Updater** — tiny standalone self-contained single-file exe (`win-x64`). The main app's `.csproj` has an MSBuild target (`BuildAndCopyUpdater`) that builds this project and copies its output into `AMS2ChEd`'s own `bin/.../Updater/` folder after every build — so the updater is always rebuilt as part of building the main app.
- **AMS2ChEd.Tests** — MSTest + Moq unit tests, covering `AMS2ChEd.Business` logic only (no UI/AMS2-specific tests). Methods run in parallel (`[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` in `MSTestSettings.cs`), so tests must not share mutable static state.

### The Business / Business.AMS2 split

Most core logic types in `AMS2ChEd.Business` are written as a template-method base class with `protected virtual` extension points (e.g. `GameEngine.InitializeConcretePlayerDriverData`), overridden in `Ams2ChEd.Business.AMS2` (e.g. `Ams2GameEngine`) to inject AMS2-specific fields (helmet file paths, etc.). When extending shared game logic, prefer adding a virtual hook in the Business base class and overriding it in the AMS2 subclass over branching on game type inside the base class. `DriversLoader`/`ISeasonLoader<TSeason>`/etc. follow the same generic-base + AMS2-concrete-type pattern (`Ams2DriverData`, `Ams2Season`, `Ams2TeamEntry`).

Conversions between a base interface instance and its AMS2-concrete subtype go through `CopyFieldsToChildClassExtension.ConvertToChild<TBase,TDerived>()` (reflection-based property copy). Deep copies of domain objects go through `DeepCloneExtension.DeepClone<T>()` (serialize/deserialize round-trip via `DefaultJsonSerializerOptions`) — this is the established pattern for cloning seasons/drivers/save games, not manual member-wise copies.

### The game-module seam

A newer, coarser-grained seam sits alongside the template-method pattern above: `AMS2ChEd.Business.DependencyInjection.IGameModule` is the compile-time boundary for "which game." `App.xaml.cs::ConfigureServices` is documented as "the only place that touches `Ams2ChEd.Business.AMS2`" outside of the AMS2 project itself — it constructs one `Ams2GameModule` and calls `RegisterServices`, which is where every AMS2-concrete registration (loaders, `Ams2GameEngine`, `Ams2RacePreparator`, `Ams2RaceDataService`, `SeasonModInstaller`, etc.) lives. `AMS2ChEd.SeasonPackCreator/App.xaml.cs` registers a similar but smaller subset by hand (it doesn't use `IGameModule` itself, but does depend on the same AMS2 concrete types and on `IGameInstallSettingsStorage`).

Game-agnostic contracts introduced for this seam, all in `AMS2ChEd.Business`:
- `IGameDataFactory` (`DependencyInjection/`) — bundles `IDriversLoader`/`ITeamsLoader`/`ISeasonLoader`/`IGameStorage`/`IAccoladesLoader`; implemented by `Ams2StorageFactory` in the AMS2 project.
- `GameLogicFactory` — bundles the game-agnostic logic services (`IStandingsManager`, `IGameEngine`, `IRacePreparator`, `IRaceDataService`, `IRaceSetupAdvisor`, etc.) into one injectable object for windows that need several at once.
- `IRaceSetupAdvisor` — AI-difficulty suggestions and pre-qualifying pool rebasing (`Ams2RaceSetupAdvisor`).
- `IPlayerCosmeticsEditor` — optional per-game cosmetics (e.g. helmet) editing. Resolved with `GetService` (nullable), not `GetRequiredService` — a game with no cosmetics concept simply doesn't register one; `MainWindow` hides the helmet-selection UI when it's null. Implemented by `Ams2PlayerCosmeticsEditor`.
- `IOffSeasonOrchestrator` / `IOffSeasonUiCallbacks` — sequences the off-season saga (contract letters, team applications, retirement news, new-season roster reveal) while keeping the step logic UI-framework-agnostic; `OffSeasonOrchestrator` (Business) drives it, the AMS2ChEd views implement `IOffSeasonUiCallbacks` to actually show dialogs.
- `ISeasonPackInstaller` — `PackFileExtension`/`PackFileFilterLabel`/`InstallSeasonMod`, implemented by `SeasonModInstaller`. Replaces hardcoded `.rwgp` checks so a future game module could ship a different pack format.
- `IGameInstallSettingsStorage` (`Settings/Contracts`) — game-agnostic `GameInstallFolder`/`PlayerInGameName`, implemented by `Ams2GameInstallSettingsStorage`. This is newer and narrower than the older AMS2-specific `IAms2AppSettingsStorage`/`Ams2SettingsStorage`, which some older dialogs (e.g. `PerformanceCalibrationDialog`) still consume directly for AMS2-only settings — both currently coexist.

`--scenariocreatormode` (mock race data without AMS2 running) was removed from `App.xaml.cs` during this refactor (see "remove scenario creator mode" commit). `StubRacePreparator` and `MockUserControlledRaceDataService` still exist in `AMS2ChEd.Business` but are no longer instantiated anywhere — treat them as orphaned unless/until they're rewired through `IGameModule` or deleted.

## Commands

```powershell
# Build everything (also rebuilds & copies the Updater into AMS2ChEd's output)
dotnet build AMS2ChEd/AMS2ChEd.sln -c Debug

# Run all unit tests
dotnet test AMS2ChEd.Tests/AMS2ChEd.Tests.csproj

# Run a single test class or method
dotnet test AMS2ChEd.Tests/AMS2ChEd.Tests.csproj --filter ClassName=EndOfSeasonManagerTests
dotnet test AMS2ChEd.Tests/AMS2ChEd.Tests.csproj --filter "FullyQualifiedName~EndOfSeasonManagerTests.SomeMethod"

# Run the app (needs Windows; launches the WPF UI)
dotnet run --project AMS2ChEd/AMS2ChEd.csproj
```

Useful app launch flags (checked in `App.xaml.cs::OnStartup` / `e.Args`):
- `--forceupdate` — forces the app-version-check flow.
- `--forceseasonsupdate` — forces re-checking/re-downloading season packs from the manifest.
- `--developermode` — feeds `DeveloperModeSettings.IsEnabled`, which currently just reveals the "Developer Tools" panel in `MainWindow` (in-sim performance calibration, etc.); it does not swap in any mock race data.
- A pack file path (extension given by `ISeasonPackInstaller.PackFileExtension`, `.rwgp` for AMS2) as the first arg triggers `InstallSeasonMod` on startup (this is also how Windows file association for `.rwgp` invokes the app — see `FileAssociationHelper.Register`).

## Data and storage layout

All paths are relative to `AppDomain.CurrentDomain.BaseDirectory` (or `%LocalAppData%`), now split across two helpers post-decoupling:
- `AMS2ChEd.Business.Storage.AppPaths` — the genuinely game-agnostic subset: `SeasonsFolder`, `SeasonsManifestPath(fileName)` (each `IGameModule` supplies its own manifest filename via `SeasonsManifestFileName`, e.g. AMS2's `seasons_manifest.json`), `SavesFolder`, `CurrentVersionCheckPath`.
- `Ams2ChEd.Business.AMS2.Helpers.StoragePaths` — everything still AMS2-specific: per-year `SeasonFilePath`/`DriversFilePath`/`AccoladesFilePath`/`ExternalLiveriesFilePath`, `TeamsFilePath`, `CarModelCapacitiesFilePath`, `BaseHelmetLiveriesPath`.

Check/extend `AppPaths` for new data any future game module would also need; extend AMS2's `StoragePaths` for anything AMS2-format-specific.

- `Seasons/<year>/season.json`, `Seasons/<year>/drivers.json` — season-pack data (teams, races, points system, drivers, reputations).
- `Seasons/<year>/{car_liveries,helmet_liveries,helmet_sponsors,liveries_xml,static_assets,scenarios,previews}/` — per-season art/template assets, copied into the season pack zip by `SeasonModInstaller` and consumed by `Ams2LiveryService` when generating race files.
- `seasons_manifest.json` — catalog of available/installed season packs, read by `SeasonManifestService`.
- `Saves/` — player save games (`GameStorage`, plain JSON of `SaveGame`).
- `Teams/teams.json` — global team roster data shared across seasons.
- `%LocalAppData%/RewindGP/preferences.json` — app-level settings/version-check state (outside the app folder, survives reinstalls).

At race time, `Ams2RacePreparator`/`Ams2LiveryService` write generated liveries, custom AI roster XML, and DDS textures **directly into the user's AMS2 game installation** (`UserData/CustomAIDrivers/*.xml`, `Vehicles/Textures/CustomLiveries/Overrides/...`), using the AMS2 install path resolved via `IGameInstallSettingsStorage`/`Ams2GameInstallSettingsStorage` (game-agnostic path; see [The game-module seam](#the-game-module-seam)) — some older code still reads it via the AMS2-specific `IAms2AppSettingsStorage`/`Ams2SettingsStorage` instead. Be careful with changes here — bugs can write into a real game install.

## Live game integration

`Ams2RaceDataService` (`Ams2ChEd.Business.AMS2/Services`) polls AMS2's shared memory (`AMS2SharedMemoryNet`, mapped file name `$pcars2$`) on a background loop every 500ms while a session is active, decodes participant/session data, and raises `SessionUpdated`/`SessionFinished` events that the UI and game-logic layer consume to detect when quali/race sessions finish and pull final standings. This can only be exercised with AMS2 actually running — there's currently no supported way to test it without the game (the old `--scenariocreatormode` mock path was removed; see [The game-module seam](#the-game-module-seam)).
