# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

"Rewind GP" (assembly/repo name `AMS2ChEd` — AMS2 Championship Editor) is a Windows WPF career-mode manager for **Automobilista 2** (AMS2). It lets a player create a driver, get hired/replace a driver on a historical grid (e.g. 1996, 1997 F1 seasons), play through a season race-by-race against AMS2's AI, and have the app handle everything the game itself doesn't: contract negotiations, driver reputation, end-of-season driver movements, standings, absences, and generating the liveries/AI roster files AMS2 needs for each race weekend. It reads live session results out of AMS2 via shared memory while a race weekend is in progress.

Season content (drivers, teams, liveries, helmets, calendars) ships as data-driven "season packs" under `AMS2ChEd/Seasons/<year>/`, distributed/updated separately from app code (see `seasons_manifest.json` and the Updater section below) and authored with the companion `AMS2ChEd.SeasonPackCreator` tool.

## Solution layout

Open `AMS2ChEd/AMS2ChEd.sln`. Six projects, all targeting `net8.0` / `net8.0-windows`:

- **AMS2ChEd** — the main WPF app (`OutputType=WinExe`). MVVM-ish: `Views/` (XAML windows) + `Views/*.xaml.cs` code-behind drive most logic directly; `ViewModels/` and `Commands/` (a basic `RelayCommand`) are used for newer/simpler windows. DI is wired by hand in `App.xaml.cs::ConfigureServices` using `Microsoft.Extensions.DependencyInjection` — there is no auto-registration, so a new service/window must be registered there to be resolved via constructor injection.
- **AMS2ChEd.Business** — game-agnostic domain layer. Defines interfaces (`GameLogic/Contracts`, `Services/Contracts`, `Storage/Contracts`) and base/default implementations (`GameLogic/Concrete`, `Services/`) for career-mode logic: contracts, reputation, standings, end-of-season driver movement, race-number allocation, the season-pack update pipeline (`Updater/`).
- **Ams2ChEd.Business.AMS2** — AMS2-specific layer. Subclasses/implements the Business contracts for AMS2 (`Ams2GameEngine`, `Ams2RacePreparator`, `Ams2RaceDataService`, etc.), and owns everything AMS2-file-format specific: JSON storage (`Storage/Concrete/JsonStorage`), livery/DDS generation (`Services/Ams2LiveryService.cs`, `Helpers/DdsTextureComposer.cs`), helmet picking, and the AMS2 shared-memory telemetry reader. Carries `ExternalDependencies/AMS2SharedMemoryNet.dll`, a third-party binary (no source) for reading AMS2's `$pcars2$` shared-memory block — referenced as a plain `<Reference>`, not a NuGet package.
- **AMS2ChEd.SeasonPackCreator** — separate WPF tool for authoring new season packs: driver/team/race/livery editors, a 3D helmet editor (HelixToolkit), and a `JolpicaF1Service` that pulls real historical F1 data from the Jolpica API to bootstrap season data. Not needed to work on the main app.
- **AMS2ChEd.Updater** — tiny standalone self-contained single-file exe (`win-x64`). The main app's `.csproj` has an MSBuild target (`BuildAndCopyUpdater`) that builds this project and copies its output into `AMS2ChEd`'s own `bin/.../Updater/` folder after every build — so the updater is always rebuilt as part of building the main app.
- **AMS2ChEd.Tests** — MSTest + Moq unit tests, covering `AMS2ChEd.Business` logic only (no UI/AMS2-specific tests). Methods run in parallel (`[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` in `MSTestSettings.cs`), so tests must not share mutable static state.

### The Business / Business.AMS2 split

Most core logic types in `AMS2ChEd.Business` are written as a template-method base class with `protected virtual` extension points (e.g. `GameEngine.InitializeConcretePlayerDriverData`), overridden in `Ams2ChEd.Business.AMS2` (e.g. `Ams2GameEngine`) to inject AMS2-specific fields (helmet file paths, etc.). When extending shared game logic, prefer adding a virtual hook in the Business base class and overriding it in the AMS2 subclass over branching on game type inside the base class. `DriversLoader`/`ISeasonLoader<TSeason>`/etc. follow the same generic-base + AMS2-concrete-type pattern (`Ams2DriverData`, `Ams2Season`, `Ams2TeamEntry`).

Conversions between a base interface instance and its AMS2-concrete subtype go through `CopyFieldsToChildClassExtension.ConvertToChild<TBase,TDerived>()` (reflection-based property copy). Deep copies of domain objects go through `DeepCloneExtension.DeepClone<T>()` (serialize/deserialize round-trip via `DefaultJsonSerializerOptions`) — this is the established pattern for cloning seasons/drivers/save games, not manual member-wise copies.

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
- `--scenariocreatormode` — swaps in `StubRacePreparator` / `MockUserControlledRaceDataService` instead of the real AMS2 shared-memory integration, for testing scenarios without the game running.
- `--forceupdate` — forces the app-version-check flow.
- `--forceseasonsupdate` — forces re-checking/re-downloading season packs from the manifest.
- A `.rwgp` file path as the first arg triggers `SeasonModInstaller.InstallSeasonMod` on startup (this is also how Windows file association for `.rwgp` invokes the app — see `FileAssociationHelper.Register`).

## Data and storage layout

All paths are resolved relative to `AppDomain.CurrentDomain.BaseDirectory` via `Ams2ChEd.Business.AMS2.Helpers.StoragePaths` (the one place to check/extend when adding new on-disk data):
- `Seasons/<year>/season.json`, `Seasons/<year>/drivers.json` — season-pack data (teams, races, points system, drivers, reputations).
- `Seasons/<year>/{car_liveries,helmet_liveries,helmet_sponsors,liveries_xml,static_assets,scenarios,previews}/` — per-season art/template assets, copied into the season pack zip by `SeasonModInstaller` and consumed by `Ams2LiveryService` when generating race files.
- `seasons_manifest.json` — catalog of available/installed season packs, read by `SeasonManifestService`.
- `Saves/` — player save games (`GameStorage`, plain JSON of `SaveGame`).
- `Teams/teams.json` — global team roster data shared across seasons.
- `%LocalAppData%/RewindGP/preferences.json` — app-level settings/version-check state (outside the app folder, survives reinstalls).

At race time, `Ams2RacePreparator`/`Ams2LiveryService` write generated liveries, custom AI roster XML, and DDS textures **directly into the user's AMS2 game installation** (`UserData/CustomAIDrivers/*.xml`, `Vehicles/Textures/CustomLiveries/Overrides/...`), using the AMS2 install path configured in `Ams2AppSettings.Ams2Folder` (`SettingsStorage`/`IAms2AppSettingsStorage`). Be careful with changes here — bugs can write into a real game install.

## Live game integration

`Ams2RaceDataService` (`Ams2ChEd.Business.AMS2/Services`) polls AMS2's shared memory (`AMS2SharedMemoryNet`, mapped file name `$pcars2$`) on a background loop every 500ms while a session is active, decodes participant/session data, and raises `SessionUpdated`/`SessionFinished` events that the UI and game-logic layer consume to detect when quali/race sessions finish and pull final standings. This can only be exercised with AMS2 actually running — there's no way to test it without the game, which is why `--scenariocreatormode` exists with a mock (`MockUserControlledRaceDataService`) for working on the rest of the flow without launching AMS2.
