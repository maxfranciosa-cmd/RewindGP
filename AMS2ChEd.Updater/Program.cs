using System.Diagnostics;
using System.IO.Compression;
using System.Text;

Console.WriteLine("Press any key to install Rewind GP update...");
Console.ReadKey(true);

try
{
    var parts = args[0].Split('|');
    var targetPid = int.Parse(parts[0]);
    var installDir = parts[1];
    var zipPath = parts[2];
    var originalArgs = parts[3];
    var version = parts[4];

    // Wait for RewindGP.exe to fully exit
    try
    {
        var process = Process.GetProcessById(targetPid);
        await Task.Run(() => process.WaitForExit(10_000));
    }
    catch (ArgumentException) { /* already exited */ }

    await Task.Delay(500); // small buffer

    try
    {
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var exeEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("AMS2ChEd.exe", StringComparison.OrdinalIgnoreCase));

            if (exeEntry == null)
            {
                Console.WriteLine("ERROR: AMS2ChEd.exe not found in ZIP.");
                Console.ReadKey(true);
                return;
            }

            var tempExe = Path.Combine(Path.GetTempPath(), $"AMS2ChEd-check-{Guid.NewGuid()}.exe");
            try
            {
                exeEntry.ExtractToFile(tempExe);
                var exeVersion = FileVersionInfo.GetVersionInfo(tempExe);
                var exeVersionString = $"{exeVersion.FileMajorPart}.{exeVersion.FileMinorPart}";
                Console.WriteLine($"ZIP contains version: {exeVersionString}");

                if (exeVersionString != version)
                {
                    Console.WriteLine($"ERROR: Expected version {version}");
                    Console.ReadKey(true);
                    return;
                }
            }
            finally
            {
                if (File.Exists(tempExe)) File.Delete(tempExe);
            }

            foreach (var entry in archive.Entries)
            {

                // Skip directory entries
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Console.WriteLine($"skipping entry {entry.FullName} as Name={entry.Name}");
                    continue;
                }

                // Normalise path separators for comparison
                var relativePath = entry.FullName.Replace('\\', '/');

                // Never touch installed season packs
                if (relativePath.StartsWith("Seasons/", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"skipping entry {entry.FullName} as entry.FullName.Replace('\\', '/')={relativePath}");
                    continue;
                }

                var destPath = Path.Combine(installDir, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                Console.WriteLine($"extract {entry.FullName} as {destPath}");
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

            

        // Relaunch main app
        Process.Start(new ProcessStartInfo(
            Path.Combine(installDir, $"AMS2ChEd.exe"))
        { UseShellExecute = true, Arguments = originalArgs });
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
    finally
    {
        File.Delete(zipPath); // clean up downloaded zip
    }
}
finally
{
    Console.WriteLine($"press any key to exit.");
    Console.ReadKey(true);
}
