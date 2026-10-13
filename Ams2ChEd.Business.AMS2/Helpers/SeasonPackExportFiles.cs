using AMS2ChEd.Business.AMS2.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Names and generated text files of a season livery pack export (see
    /// Ams2LiveryService.ExportSeasonPack): pure functions, kept apart from the texture work so
    /// they can be tested on their own.
    /// </summary>
    public static class SeasonPackExportFiles
    {
        public static string SetupsFolderName(int year) => $"RewindGP_{year}_RaceSetups";

        public static string SelectorScriptName(int year) => $"RewindGP {year} - choose race.bat";

        public static string ReadmeName(int year) => $"RewindGP {year} - README.txt";

        /// <summary>
        /// The tag a team's textures carry at a race where it runs a livery override: empty when
        /// it doesn't, otherwise "_r" plus the first race that uses the very same override. One
        /// export holds every race's textures side by side, so an override's numbered livery or
        /// sponsored helmet must not overwrite the base one - while races sharing an identical
        /// override (a three-race special livery, say) still share one set of files.
        /// </summary>
        public static string OverrideTextureTag(IEnumerable<LiveryOverride> overrides, int raceId)
        {
            var all = overrides?.ToList() ?? new List<LiveryOverride>();
            var current = all.FirstOrDefault(o => o.RaceId == raceId);
            if (current == null) return string.Empty;

            string key = ContentKey(current);
            int firstRaceId = all.Where(o => ContentKey(o) == key).Min(o => o.RaceId);
            return string.Format(CultureInfo.InvariantCulture, "_r{0}", firstRaceId);
        }

        /// <summary>
        /// "01 - Brazilian Grand Prix": the round first, so folders sort in calendar order, and
        /// only characters a .bat menu can echo and copy safely (no accents, brackets, '!', '&amp;' or '%').
        /// </summary>
        public static string RaceFolderName(int round, string raceName)
        {
            var decomposed = (raceName ?? string.Empty).Normalize(NormalizationForm.FormD);
            var safe = new StringBuilder();
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c < 128 && (char.IsLetterOrDigit(c) || c is ' ' or '-' or '.' or '\''))
                    safe.Append(c);
                else if (char.IsWhiteSpace(c))
                    safe.Append(' ');
            }

            string name = string.Join(' ', safe.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
            if (name.Length == 0) name = "Race";
            return string.Format(CultureInfo.InvariantCulture, "{0:00} - {1}", round, name);
        }

        /// <summary>
        /// The race picker: lists the race folders, asks for a number and copies that race's
        /// livery and Custom AI XMLs over the installed ones. Expects to sit in the Automobilista 2
        /// folder next to the race-setups folder, which is where the pack is extracted. Windows
        /// line endings, plain ASCII.
        /// </summary>
        public static string BuildSelectorScript(int year, string setupsFolderName)
        {
            var lines = new[]
            {
                "@echo off",
                "setlocal EnableExtensions EnableDelayedExpansion",
                $"title RewindGP {year} - choose a race",
                "cd /d \"%~dp0\"",
                $"set \"SETUPS={setupsFolderName}\"",
                "",
                "if not exist \"%SETUPS%\\\" (",
                "    echo The \"%SETUPS%\" folder wasn't found next to this file.",
                "    echo Copy the whole pack into your Automobilista 2 folder first.",
                "    echo.",
                "    pause",
                "    exit /b 1",
                ")",
                "",
                $"echo RewindGP {year} livery pack",
                "echo Choose the race to recreate: its AI drivers and liveries will be installed.",
                "echo.",
                "set /a COUNT=0",
                "for /d %%D in (\"%SETUPS%\\*\") do (",
                "    set /a COUNT+=1",
                "    set \"RACE!COUNT!=%%~nxD\"",
                "    echo   !COUNT!. %%~nxD",
                ")",
                "echo.",
                "",
                "set \"CHOICE=\"",
                "set /p \"CHOICE=Race number: \"",
                "if not defined CHOICE goto invalid",
                "set \"CHOSEN=\"",
                "for /l %%N in (1,1,!COUNT!) do if \"!CHOICE!\"==\"%%N\" set \"CHOSEN=!RACE%%N!\"",
                "if not defined CHOSEN goto invalid",
                "",
                "xcopy \"%SETUPS%\\!CHOSEN!\\*\" \".\" /E /Y /I /Q >nul",
                "if errorlevel 1 (",
                "    echo.",
                "    echo Copying the files failed. If Automobilista 2 is installed under Program Files,",
                "    echo right-click this file and choose Run as administrator.",
                "    echo.",
                "    pause",
                "    exit /b 1",
                ")",
                "",
                "echo.",
                "echo Installed: !CHOSEN!",
                "echo Set up a Custom Race with this car class and the AI drivers will match it.",
                "echo.",
                "pause",
                "exit /b 0",
                "",
                ":invalid",
                "echo.",
                "echo That isn't one of the race numbers above.",
                "echo.",
                "pause",
                "exit /b 1",
            };
            return string.Join("\r\n", lines) + "\r\n";
        }

        public static string BuildReadme(int year, string ams2Class, IReadOnlyList<string> raceFolders, string setupsFolderName, string selectorScriptName)
        {
            var lines = new List<string>
            {
                $"RewindGP {year} livery pack",
                "",
                "Installing",
                "  Copy everything in this folder into your Automobilista 2 folder",
                "  (the one holding AMS2AVX.exe), and confirm replacing files.",
                $"  The first race, {raceFolders[0]}, is installed straight away.",
                "",
                "Recreating another race",
                $"  Double-click \"{selectorScriptName}\" in the Automobilista 2 folder and type the race number.",
                "  It installs that race's AI drivers (with any substitutes) and liveries (with any special",
                "  liveries that race had).",
                "",
                "Racing",
                $"  Start a Custom Race with the {ams2Class} class and choose the custom AI drivers.",
                "",
                $"Races ({setupsFolderName})",
            };
            lines.AddRange(raceFolders.Select(r => "  " + r));
            return string.Join("\r\n", lines) + "\r\n";
        }

        private static string ContentKey(LiveryOverride o) =>
            JsonSerializer.Serialize(new
            {
                o.Driver1Livery,
                o.Driver2Livery,
                o.HelmetSponsors,
                o.VisorSponsors,
                o.DriversSpecificHelmet,
                o.NumbersPlacements,
                o.LiveryPreview
            });
    }
}
