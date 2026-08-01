# Rewind GP (AMS2ChEd)

A Windows WPF career-mode manager for **Automobilista 2**. Create a driver, get hired onto (or replace a driver on) a historical grid — e.g. the 1996 or 1997 F1 season — and play through it race by race against AMS2's AI. Rewind GP handles everything the game itself doesn't: contract negotiations, driver reputation, end-of-season driver movements, standings, absences, and generating the liveries/AI roster files AMS2 needs for each race weekend. It reads live session results out of AMS2 via shared memory while a race weekend is in progress.

Season content (drivers, teams, liveries, helmets, calendars) ships as data-driven "season packs" distributed and updated separately from the app, authored with the companion Season Pack Creator tool.

## Solution layout

Open `AMS2ChEd/AMS2ChEd.sln`. Six projects, all targeting `net8.0` / `net8.0-windows`:

- **AMS2ChEd** — the main WPF app.
- **AMS2ChEd.Business** — game-agnostic domain layer (contracts, reputation, standings, end-of-season driver movement, race-number allocation, season-pack update pipeline).
- **Ams2ChEd.Business.AMS2** — AMS2-specific layer: JSON storage, livery/DDS generation, helmet picking, and the AMS2 shared-memory telemetry reader.
- **AMS2ChEd.SeasonPackCreator** — separate WPF tool for authoring new season packs (driver/team/race/livery editors, a 3D helmet editor, and historical F1 data import).
- **AMS2ChEd.Updater** — standalone self-contained updater exe, built and bundled into the main app automatically.
- **AMS2ChEd.Tests** — MSTest + Moq unit tests covering `AMS2ChEd.Business` logic.

See `CLAUDE.md` for a deeper architectural overview.

## Commands

```powershell
# Build everything (also rebuilds & copies the Updater into AMS2ChEd's output)
dotnet build AMS2ChEd/AMS2ChEd.sln -c Debug

# Run all unit tests
dotnet test AMS2ChEd.Tests/AMS2ChEd.Tests.csproj

# Run the app (needs Windows; launches the WPF UI)
dotnet run --project AMS2ChEd/AMS2ChEd.csproj
```

## License

See `LICENSE`.
