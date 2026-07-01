using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using SixLabors.ImageSharp;
using System.Text.Json;
using System.Xml.Linq;

namespace Ams2ChEd.Business.AMS2.Services
{
    /// <summary>
    /// Service for generating AMS2 livery XML files, custom AI files, and helmet/visor DDS textures
    /// </summary>
    public class Ams2LiveryService
    {
        private readonly IEnumerable<Ams2DriverData> driversData;
        private readonly int seasonYear;
        private readonly string ams2Class;
        private readonly Dictionary<string, Ams2TeamEntry> teamsDict;
        private readonly Dictionary<string, Ams2DriverData> driversDict;

        /// <summary>
        /// Initialize the LiveryService
        /// </summary>
        /// <param name="driversJsonPath">Path to drivers.json</param>
        /// <param name="seasonJsonPath">Path to season.json</param>
        /// <param name="ddsComposerFunction">Function to compose DDS files: (baseHelmetPath, sponsorPath, outputPath) => outputPath</param>
        public Ams2LiveryService(
            int seasonYear,
            string ams2Class,
            IEnumerable<Ams2DriverData> driversData,
            IEnumerable<Ams2TeamEntry> teamsData)
        {

            this.driversData = driversData;
            this.seasonYear = seasonYear;
            this.teamsDict = teamsData.ToDictionary(t => t.TeamId);
            this.ams2Class = ams2Class;

            // Create driver lookup dictionary
            driversDict = driversData.ToDictionary(d => d.DriverId, d => (Ams2DriverData)d);
        }

        /// <summary>
        /// Generate all outputs for a specific race using AMS2 folder structure
        /// </summary>
        /// <param name="raceId">The race ID</param>
        /// <param name="raceEntryList">List of entries with team and driver assignments</param>
        /// <param name="seasonDirectory">Season directory (e.g., Seasons/1996) containing static_assets/, car_liveries/, helmet_sponsors/</param>
        /// <param name="ams2RootDirectory">Root AMS2 directory (contains UserData and Vehicles folders) - all outputs go here</param>
        /// <param name="playerData">Optional player data (if a driver is the player)</param>
        public void GenerateRaceFiles(
            int raceId,
            List<EntryListEntry> raceEntryList,
            string seasonDirectory,
            string ams2RootDirectory)
        {
            GenerateLiveriesOnly(raceId, raceEntryList, seasonDirectory, ams2RootDirectory);
            GenerateCustomAiOnly(raceId, raceEntryList, ams2RootDirectory);
        }

        /// <summary>
        /// Generate only the livery XMLs, car liveries, and helmets for a specific race (no CustomAI roster)
        /// </summary>
        public void GenerateLiveriesOnly(
            int raceId,
            List<EntryListEntry> raceEntryList,
            string seasonDirectory,
            string ams2RootDirectory)
        {
            // Copy static assets AS-IS from season/static_assets to AMS2
            CopyStaticAssets(seasonDirectory, ams2RootDirectory);

            string vehiclesOverridesPath = Path.Combine(ams2RootDirectory, "Vehicles", "Textures", "CustomLiveries", "Overrides");

            // Generate livery XMLs, copy car liveries, and generate helmets
            GenerateLiveryXmlsAMS2(raceId, raceEntryList, vehiclesOverridesPath, seasonDirectory);
        }

        /// <summary>
        /// Generate only the custom AI roster XML for a specific race (no liveries)
        /// </summary>
        public void GenerateCustomAiOnly(
            int raceId,
            List<EntryListEntry> raceEntryList,
            string ams2RootDirectory)
        {
            string customAiPath = Path.Combine(ams2RootDirectory, "UserData", "CustomAIDrivers", $"{ams2Class}.xml");

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(customAiPath));

            GenerateCustomAiXml(raceId, raceEntryList, customAiPath);
        }

        /// <summary>
        /// Copy static assets from season/static_assets to AMS2 root directory
        /// </summary>
        private void CopyStaticAssets(string seasonDirectory, string ams2RootDirectory)
        {
            string staticAssetsPath = Path.Combine(seasonDirectory, "static_assets");

            if (!Directory.Exists(staticAssetsPath))
            {
                Console.WriteLine($"Warning: static_assets directory not found: {staticAssetsPath}");
                return;
            }

            Console.WriteLine($"Copying static assets from: {staticAssetsPath}");
            Console.WriteLine($"                       to: {ams2RootDirectory}");

            CopyDirectory(staticAssetsPath, ams2RootDirectory);

            Console.WriteLine("Static assets copied successfully");
        }

        /// <summary>
        /// Recursively copy directory contents
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Create destination directory if it doesn't exist
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, overwrite: true);
                Console.WriteLine($"  Copied: {fileName}");
            }

            // Recursively copy subdirectories
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(subDir, destSubDir);
            }
        }

        /// <summary>
        /// Clean up files in temporary_textures that don't match the current season year
        /// </summary>
        private void CleanupOldSeasonFiles(string temporaryTexturesPath, int currentSeasonYear)
        {
            if (!Directory.Exists(temporaryTexturesPath))
                return;

            string seasonSuffix = $"_{currentSeasonYear}.dds";
            var filesToDelete = Directory.GetFiles(temporaryTexturesPath, "*.dds")
                .Where(f => !f.EndsWith(seasonSuffix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                    Console.WriteLine($"Deleted old season file: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not delete {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            if (filesToDelete.Count > 0)
            {
                Console.WriteLine($"Cleaned up {filesToDelete.Count} old season file(s) from temporary_textures");
            }


        }

        private void ProcessDriverHelmetVisor(
            string driverId,
            Ams2TeamEntry team,
            string helmetSponsors,
            string visorSponsors,
            Dictionary<string, string> driverSpecificHelmet,
            string outputDirectory,
            string seasonDirectory)
        {
            string baseHelmetFile;
            string baseVisorFile;


            // Use AI driver's helmet and visor (resolve relative to baseHelmetLiveriesDirectory)
            if (string.IsNullOrEmpty(driverId) || !driversDict.TryGetValue(driverId, out var driver)) return;

            // Get baseHelmet and baseVisor for this season
            var baseHelmetFileToPick = HelmetPicker.PickHelmetFilePerYear(driver, seasonYear);
            var baseVisorFileToPick = HelmetPicker.PickVisorFilePerYear(driver, seasonYear);

            baseHelmetFileToPick = string.IsNullOrEmpty(baseHelmetFileToPick) ? HelmetPicker.DefaultBaseHelmetFile(seasonYear) : baseHelmetFileToPick;
            baseVisorFileToPick = string.IsNullOrEmpty(baseVisorFileToPick) ? HelmetPicker.DefaultBaseVisorFile(seasonYear) : baseVisorFileToPick;

            baseHelmetFile = Path.IsPathRooted(baseHelmetFileToPick)
                    ? baseHelmetFileToPick
                    : Path.Combine(seasonDirectory, baseHelmetFileToPick);

            baseVisorFile = baseVisorFileToPick == string.Empty || Path.IsPathRooted(baseVisorFileToPick)
                    ? baseVisorFileToPick
                    : Path.Combine(seasonDirectory, baseVisorFileToPick);

            // New naming format: [driver_id]_[team_id]_[seasonYear].dds
            string helmetOutputFilename = $"{driverId}_{team.TeamId}_{seasonYear}.dds";
            string visorOutputFilename = $"{driverId}_{team.TeamId}_{seasonYear}_visor.dds";
            string outputHelmetPath = Path.Combine(outputDirectory, helmetOutputFilename);
            string outputVisorPath = Path.Combine(outputDirectory, visorOutputFilename);

            // Resolve sponsor paths relative to seasonDirectory
            string resolvedHelmetSponsors = string.IsNullOrEmpty(helmetSponsors)
                ? null
                : (Path.IsPathRooted(helmetSponsors)
                    ? helmetSponsors
                    : Path.Combine(seasonDirectory, helmetSponsors));

            string resolvedVisorSponsors = string.IsNullOrEmpty(visorSponsors)
                ? null
                : (Path.IsPathRooted(visorSponsors)
                    ? visorSponsors
                    : Path.Combine(seasonDirectory, visorSponsors));

            if (driverSpecificHelmet != null && driverSpecificHelmet.ContainsKey(driverId))
            {
                // Just copy the specific helmet file
                var specificHelmetFile = driverSpecificHelmet[driverId];
                specificHelmetFile = Path.IsPathRooted(specificHelmetFile)
                    ? specificHelmetFile
                    : Path.Combine(seasonDirectory, specificHelmetFile);

                if (File.Exists(specificHelmetFile))
                {
                    File.Copy(specificHelmetFile, outputHelmetPath, overwrite: true);
                    Console.WriteLine($"  Copied helmet: {helmetOutputFilename}");
                }
                else
                {
                    Console.WriteLine($"Warning: Specific helmet file not found: {specificHelmetFile}");
                }
            }
            else if (!string.IsNullOrEmpty(resolvedHelmetSponsors) || Path.GetExtension(baseHelmetFile) != ".dds")
            {
                DdsTextureComposer.Compose(baseHelmetFile, resolvedHelmetSponsors, outputHelmetPath);
                Console.WriteLine($"  Generated helmet: {helmetOutputFilename}");
            }
            else if (!string.IsNullOrEmpty(baseHelmetFile))
            {
                // Just copy the base helmet if no sponsors
                if (File.Exists(baseHelmetFile))
                {
                    File.Copy(baseHelmetFile, outputHelmetPath, overwrite: true);
                    Console.WriteLine($"  Copied helmet: {helmetOutputFilename}");
                }
                else
                {
                    Console.WriteLine($"Warning: Base helmet file not found: {baseHelmetFile}");
                }
            }


            // Generate visor
            if (!string.IsNullOrEmpty(resolvedVisorSponsors))
            {
                if (!string.IsNullOrEmpty(baseVisorFile))
                {
                    DdsTextureComposer.Compose(baseVisorFile, resolvedVisorSponsors, outputVisorPath);
                    Console.WriteLine($"  Generated visor: {visorOutputFilename}");
                }
            }
            else if (!string.IsNullOrEmpty(baseVisorFile))
            {
                if (Path.GetExtension(baseVisorFile) == ".dds")
                {
                    // Just copy the base visor if no sponsors
                    if (File.Exists(baseVisorFile))
                    {
                        File.Copy(baseVisorFile, outputVisorPath, overwrite: true);
                        Console.WriteLine($"  Copied visor: {visorOutputFilename}");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Base visor file not found: {baseVisorFile}");
                    }
                }
                else
                {
                    DdsTextureComposer.Compose(baseVisorFile, null, outputVisorPath);
                }
            }
        }

        private void CopyAsDDS(string filePath, string seasonDirectory, string carModelDirectory)
        {
            // Resolve the source path (relative to seasonDirectory)
            string sourcePath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(seasonDirectory, filePath);

            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"Warning: Car livery file not found: {sourcePath}");
                return;
            }

            // Destination is just the filename in the car model directory
            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(carModelDirectory, fileName);

            if (Path.GetExtension(sourcePath) == ".dds")
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                Console.WriteLine($"  Copied car livery: {fileName}");
            }
            else
            {
                DdsTextureComposer.Compose(sourcePath, null, destPath);
                Console.WriteLine($"  Copied car livery: {fileName} as {destPath}");
            }
        }

        /// <summary>
        /// Generate livery XML files using AMS2 folder structure by loading and combining individual team XMLs
        /// </summary>
        private void GenerateLiveryXmlsAMS2(
            int raceId,
            List<EntryListEntry> raceEntryList,
            string vehiclesOverridesPath,
            string seasonDirectory)
        {

            // Group entries by car model, per driver slot (driver1/driver2 may use different models)
            var entriesByCarModel = new Dictionary<string, List<(EntryListEntry entry, Ams2TeamEntry team, int driverNumber)>>();

            foreach (var entry in raceEntryList)
            {
                Ams2TeamEntry team;
                if (!teamsDict.TryGetValue(entry.TeamId, out team)) continue;

                if (!string.IsNullOrEmpty(entry.Driver1Id))
                {
                    string carModel1 = team.GetAms2Car(1);
                    if (!entriesByCarModel.ContainsKey(carModel1))
                    {
                        entriesByCarModel[carModel1] = new List<(EntryListEntry, Ams2TeamEntry, int)>();
                    }
                    entriesByCarModel[carModel1].Add((entry, team, 1));
                }

                if (!string.IsNullOrEmpty(entry.Driver2Id))
                {
                    string carModel2 = team.GetAms2Car(2);
                    if (!entriesByCarModel.ContainsKey(carModel2))
                    {
                        entriesByCarModel[carModel2] = new List<(EntryListEntry, Ams2TeamEntry, int)>();
                    }
                    entriesByCarModel[carModel2].Add((entry, team, 2));
                }
            }

            // Process each car model
            foreach (var carModelGroup in entriesByCarModel)
            {
                string carModel = carModelGroup.Key;
                var entries = carModelGroup.Value;

                var liveryDimensions = TryGetLiveryDimensionsForCarModel(entries, seasonDirectory, raceId);
                int fallbackTextureWidth = liveryDimensions?.width ?? 512;
                int fallbackTextureHeight = liveryDimensions?.height ?? 512;

                // Build paths for this car model
                string carModelDirectory = Path.Combine(vehiclesOverridesPath, carModel);
                string outputXmlPath = Path.Combine(carModelDirectory, $"{carModel}.xml");
                string temporaryTexturesPath = Path.Combine(carModelDirectory, "temporary_textures");

                // Clean up old season files from temporary_textures folder
                if (Directory.Exists(temporaryTexturesPath))
                {
                    CleanupOldSeasonFiles(temporaryTexturesPath, seasonYear);
                }
                else
                {
                    // Create temporary_textures folder if it doesn't exist
                    Directory.CreateDirectory(temporaryTexturesPath);
                }

                // Create the combined XML document
                XDocument combinedDoc = new XDocument(new XElement("USER_OVERRIDES"));
                XElement rootElement = combinedDoc.Root;

                // Track livery number (starts at 51)
                int currentLiveryNumber = 51;

                // Generate helmet/visor DDS files for this car model
                GenerateHelmetVisorDDSForCarModel(raceId, entries, temporaryTexturesPath, seasonDirectory);

                // Process each driver slot assigned to this car model
                foreach (var (entry, team, driverNumber) in entries)
                {
                    // Get race-specific overrides
                    var raceOverride = team.LiveryOverrides?.FirstOrDefault(o => o.RaceId == raceId);

                    // Get car livery file and driver id for this slot
                    string driverLivery = driverNumber == 1
                        ? (raceOverride?.Driver1Livery ?? team.BaseLiveryDriver1)
                        : (raceOverride?.Driver2Livery ?? team.BaseLiveryDriver2);
                    string driverId = driverNumber == 1 ? entry.Driver1Id : entry.Driver2Id;
                    string previewFile = raceOverride?.LiveryPreview ?? team.LiveryPreview;

                    // Get numbers placements (with race override support)
                    var numbersPlacements = raceOverride?.NumbersPlacements ?? team.NumbersPlacements;

                    // Copy the preview livery file
                    string finalPreviewPath = Path.Combine("temporary_textures", previewFile);
                    string finalPreviewPathDirectory = Path.Combine(carModelDirectory, Path.GetDirectoryName(finalPreviewPath));
                    Directory.CreateDirectory(finalPreviewPathDirectory);
                    CopyAsDDS(previewFile, seasonDirectory, finalPreviewPathDirectory);

                    // Process this driver's livery - load individual team XML and append
                    if (!string.IsNullOrEmpty(driverId))
                        ProcessDriverLivery(
                        rootElement,
                        driverId,
                        team,
                        driverLivery,
                        finalPreviewPath,
                        driverNumber,
                        currentLiveryNumber++,
                        numbersPlacements,
                        seasonDirectory,
                        carModelDirectory,
                        temporaryTexturesPath,
                        raceId,
                        fallbackTextureWidth,
                        fallbackTextureHeight);
                }

                // Save combined XML
                combinedDoc.Save(outputXmlPath);
                Console.WriteLine($"Generated combined livery XML: {outputXmlPath}");

                // Force garbage collection after processing this car model to release memory
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            // Final garbage collection after all liveries processed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Process a single driver's livery: load team XML, apply numbers if needed, update attributes, and append to combined XML
        /// </summary>
        /// <returns>Final livery path (with or without numbers applied)</returns>
        private string ProcessDriverLivery(
            XElement rootElement,
            string driverId,
            Ams2TeamEntry team,
            string liveryFilePath,
            string finalLiveryPreviewPath,
            int driverNumber, // 1 or 2
            int liveryNumber,
            IEnumerable<NumbersPlacement> numbersPlacements,
            string seasonDirectory,
            string carModelDirectory,
            string temporaryTexturesPath,
            int raceId,
            int fallbackTextureWidth,
            int fallbackTextureHeight)
        {
            // Get driver data
            if (string.IsNullOrEmpty(driverId) || !driversDict.TryGetValue(driverId, out var driver))
            {
                Console.WriteLine($"Warning: Driver not found: {driverId}");
                return null;
            }

            // Get driver's car number from contract
            int driverCarNumber = (driverNumber == 1) ? team.Driver1Contract.DriverNumber : team.Driver2Contract.DriverNumber;

            // Path to individual team XML file
            string teamXmlPath = Path.Combine(seasonDirectory, "liveries_xml", $"{team.TeamId}.xml");

            XDocument teamDoc = null;

            if (!File.Exists(teamXmlPath))
            {
                Console.WriteLine($"Warning: Team livery XML not found: {teamXmlPath}");
                if (string.IsNullOrEmpty(team.LiveryXml))
                {
                    Console.WriteLine($"Warning: Team livery XML not found in savefile too: {team.TeamId}");
                    return null;
                }
                // load team xml from savefile
                teamDoc = XDocument.Parse(team.LiveryXml);
            }
            else
            {
                // Load team XML
                teamDoc = XDocument.Load(teamXmlPath);
            }

            XElement teamRoot = teamDoc.Root;

            if (teamRoot == null || teamRoot.Name != "USER_OVERRIDES")
            {
                Console.WriteLine($"Warning: Invalid team XML structure in {teamXmlPath}");
                return null;
            }

            // Handle livery file: apply numbers if placements defined, otherwise just copy
            string resolvedLiveryPath = string.IsNullOrEmpty(liveryFilePath)
                ? null
                : (Path.IsPathRooted(liveryFilePath)
                    ? liveryFilePath
                    : Path.Combine(seasonDirectory, liveryFilePath));

            bool hasValidTexture = !string.IsNullOrEmpty(resolvedLiveryPath) && File.Exists(resolvedLiveryPath);

            // Get numbers placements
            string finalLiveryPath;

            if (!hasValidTexture)
            {
                string fallbackFilename = $"{team.TeamId}_{driverCarNumber}_{seasonYear}.dds";
                string fallbackPath = Path.Combine(temporaryTexturesPath, fallbackFilename);

                DdsTextureComposer.GenerateSolidColour(team.Color, fallbackTextureWidth, fallbackTextureHeight, fallbackPath);

                if (numbersPlacements != null && numbersPlacements.Any())
                {
                    ApplyCarNumbers(
                        fallbackPath,
                        driverCarNumber,
                        numbersPlacements,
                        team.TeamId,
                        seasonYear,
                        seasonDirectory,
                        temporaryTexturesPath);
                }

                finalLiveryPath = Path.Combine("temporary_textures", fallbackFilename);
            }
            else if (numbersPlacements != null && numbersPlacements.Any())
            {
                finalLiveryPath = ApplyCarNumbers(
                    liveryFilePath,
                    driverCarNumber,
                    numbersPlacements,
                    team.TeamId,
                    seasonYear,
                    seasonDirectory,
                    temporaryTexturesPath);
            }
            else
            {
                CopyAsDDS(liveryFilePath, seasonDirectory, carModelDirectory);
                finalLiveryPath = Path.GetFileName(liveryFilePath);
            }

            // Process ALL child elements from the team XML
            foreach (var element in teamRoot.Elements())
            {
                // Clone the element
                var clonedElement = new XElement(element);

                // Check element type and update accordingly
                if (element.Name == "LIVERY_OVERRIDE")
                {
                    // Update LIVERY attribute
                    clonedElement.SetAttributeValue("LIVERY", liveryNumber);

                    // Update NAME attribute
                    clonedElement.SetAttributeValue("NAME", $"#{driverCarNumber} {team.TeamName} - {driver.Name}");

                    // Update BODY texture PATH if it exists
                    var bodyTexture = clonedElement.Elements("TEXTURE")
                        .FirstOrDefault(t => t.Attribute("NAME")?.Value == "BODY");

                    if (bodyTexture != null && !string.IsNullOrEmpty(finalLiveryPath))
                    {
                        bodyTexture.SetAttributeValue("PATH", finalLiveryPath);
                    }

                    // Update PREVIEW texture PATH if it exists
                    var previewImageNode = clonedElement.Elements("PREVIEWIMAGE").FirstOrDefault();
                    previewImageNode.SetAttributeValue("PATH", finalLiveryPreviewPath);
                }
                else if (element.Name == "HELMET_OVERRIDE")
                {
                    // Update LIVERY attribute
                    clonedElement.SetAttributeValue("LIVERY", liveryNumber);

                    // Update helmet BODY_DIFF texture path if it exists
                    var helmetTexture = clonedElement.Elements("TEXTURE")
                        .FirstOrDefault(t => t.Attribute("NAME")?.Value == "BODY_DIFF");

                    if (helmetTexture != null)
                    {
                        string helmetFilename = $"{driverId}_{team.TeamId}_{seasonYear}.dds";
                        string relativePath = Path.Combine("temporary_textures", helmetFilename);
                        helmetTexture.SetAttributeValue("PATH", relativePath);
                    }

                    // Update visor VISOR_DIFF texture path if it exists
                    var visorTexture = clonedElement.Elements("TEXTURE")
                        .FirstOrDefault(t => t.Attribute("NAME")?.Value == "VISOR_DIFF");

                    if (visorTexture != null)
                    {
                        string visorFilename = $"{driverId}_{team.TeamId}_{seasonYear}_visor.dds";
                        string relativePath = Path.Combine("temporary_textures", visorFilename);
                        visorTexture.SetAttributeValue("PATH", relativePath);
                    }
                }
                else
                {
                    // For any other element type, just update the livery number
                    clonedElement.SetAttributeValue("LIVERY", liveryNumber);
                }


                // Append to combined XML
                rootElement.Add(clonedElement);
            }

            Console.WriteLine($"  Added livery #{liveryNumber}: {driver.Name} ({team.TeamId})");

            return finalLiveryPath;
        }

        /// <summary>
        /// Apply car numbers to a livery texture using the NumbersPlacement specifications
        /// </summary>
        /// <returns>The relative path of the livery with numbers applied</returns>
        private string ApplyCarNumbers(
            string baseLiveryPath,
            int carNumber,
            IEnumerable<NumbersPlacement> numbersPlacements,
            string teamId,
            int seasonYear,
            string seasonDirectory,
            string temporaryTexturesPath)
        {
            // Resolve base livery path
            string resolvedLiveryPath = Path.IsPathRooted(baseLiveryPath)
                ? baseLiveryPath
                : Path.Combine(seasonDirectory, baseLiveryPath);

            if (!File.Exists(resolvedLiveryPath))
            {
                Console.WriteLine($"Warning: Base livery not found for number application: {resolvedLiveryPath}");
                return Path.GetFileName(baseLiveryPath);
            }

            // New naming format: [team_id]_[race_number]_[seasonYear].dds
            string outputFilename = $"{teamId}_{carNumber}_{seasonYear}.dds";
            string outputPath = Path.Combine(temporaryTexturesPath, outputFilename);

            // Build placement data list
            var placements = new List<DdsTextureComposer.NumberPlacementData>();

            foreach (var placement in numbersPlacements)
            {
                // Parse rotation
                int rotation = placement.NumberRotation switch
                {
                    NumberRotation.Deg0 => 0,
                    NumberRotation.Deg90 => 90,
                    NumberRotation.Deg180 => 180,
                    NumberRotation.Deg270 => 270,
                    _ => 0
                };

                placements.Add(new DdsTextureComposer.NumberPlacementData
                {
                    NumbersTexture = placement.NumbersTexture,
                    PlateWidth = placement.NumberPlateWidth,
                    StartX = placement.StartingPoint.X,
                    StartY = placement.StartingPoint.Y,
                    Rotation = rotation,
                    FillColor = placement.FillColor
                });
            }

            // Apply all numbers at once
            DdsTextureComposer.ApplyCarNumbers(
                resolvedLiveryPath,
                placements,
                carNumber,
                seasonDirectory,
                outputPath);

            Console.WriteLine($"  Applied car number {carNumber} to livery: {outputFilename}");

            // Return relative path from car model directory
            return Path.Combine("temporary_textures", outputFilename);
        }

        /// <summary>
        /// Generate helmet and visor DDS files for entries using a specific car model
        /// </summary>
        private void GenerateHelmetVisorDDSForCarModel(
            int raceId,
            List<(EntryListEntry entry, Ams2TeamEntry team, int driverNumber)> entries,
            string outputDirectory,
            string seasonDirectory)
        {
            foreach (var (entry, team, driverNumber) in entries)
            {
                // Get race-specific overrides
                var raceOverride = team.LiveryOverrides?.FirstOrDefault(o => o.RaceId == raceId);

                string driverId = driverNumber == 1 ? entry.Driver1Id : entry.Driver2Id;

                ProcessDriverHelmetVisor(
                    driverId,
                    team,
                    raceOverride?.HelmetSponsors ?? team.HelmetSponsors,
                    raceOverride?.VisorSponsors ?? team.VisorSponsors,
                    raceOverride?.DriversSpecificHelmet ?? team.DriversSpecificHelmet,
                    outputDirectory,
                    seasonDirectory);
            }
        }

        /// <summary>
        /// Generate the custom AI XML file
        /// </summary>
        private void GenerateCustomAiXml(
            int raceId,
            List<EntryListEntry> raceEntryList,
            string outputCustomAiXmlPath)
        {
            XDocument doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"));
            XElement root = new XElement("custom_ai_drivers");

            foreach (var entry in raceEntryList)
            {
                Ams2TeamEntry team;
                if (!teamsDict.TryGetValue(entry.TeamId, out team)) continue;

                // Add driver 1
                if (!string.IsNullOrEmpty(entry.Driver1Id))
                    AddDriverAiEntry(root, entry.Driver1Id, entry.Driver1Number, team);

                // Add driver 2
                if (!string.IsNullOrEmpty(entry.Driver2Id))
                    AddDriverAiEntry(root, entry.Driver2Id, entry.Driver2Number, team);
            }

            doc.Add(root);
            doc.Save(outputCustomAiXmlPath);
        }

        private void AddDriverAiEntry(XElement root, string driverId, int driverNumber, Ams2TeamEntry team)
        {
            string driverName;
            string nationality;
            Dictionary<string, double> ratings;

            // Use AI driver data with team performance malus
            if (!driversDict.TryGetValue(driverId, out var driver)) return;

            driverName = driver.Name;
            nationality = driver.Nationality;

            // Apply team performance malus to ratings

            if (driver.RatingValues == null)
            {
                // if ratings are NULL it means it's the player, so let's put generic values.
                ratings = new Dictionary<string, double>
                {
                    { "aggression", 0.5 },
                    { "avoidance_of_forced_mistakes", 0.5 },
                    { "avoidance_of_mistakes", 0.5 },
                    { "blue_flag_conceding", 0.5 },
                    { "consistency", 0.5 },
                    { "defending", 0.5 },
                    { "fuel_management", 0.5 },
                    { "qualifying_skill", 0.5 },
                    { "race_skill", 0.5 },
                    { "stamina", 0.5 },
                    { "start_reactions", 0.5 },
                    { "tyre_management", 0.5 },
                    { "weather_tyre_changes", 0.5 },
                    { "wet_skill", 0.5 }
                };
            }
            else
            {
                ratings = new Dictionary<string, double>(driver.RatingValues);
            }

            // Apply team performance malus if exists (driver-2-specific car malus if defined)
            var carPerformanceMalus = team.GetAms2CarPerformanceMalus(driverNumber);
            foreach (var malusKvp in carPerformanceMalus)
            {
                string ratingName = malusKvp.Key;
                double malus = malusKvp.Value;

                double baseValue = 0;
                
                if (ratings.ContainsKey(ratingName))
                {
                    baseValue = ratings[ratingName];
                }

                double adjustedValue = Math.Max(0.0, baseValue - malus);
                ratings[ratingName] = adjustedValue;
            }

            // Create driver element
            string liveryName = $"#{driverNumber} {team.TeamName} - {driverName}";
            XElement driverElement = new XElement("driver",
                new XAttribute("livery_name", liveryName)
            );

            // Add ratings
            foreach (var ratingKvp in ratings.OrderBy(kvp => kvp.Key))
            {
                driverElement.Add(new XElement(ratingKvp.Key, ratingKvp.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
            }

            // Add driver name
            driverElement.Add(new XElement("name", driverName));

            // Add country
            driverElement.Add(new XElement("country", nationality));

            root.Add(driverElement);
        }

        private (int width, int height)? TryGetLiveryDimensionsForCarModel(List<(EntryListEntry entry, Ams2TeamEntry team, int driverNumber)> entries, string seasonDirectory, int raceId)
        {
            foreach (var (entry, team, driverNumber) in entries)
            {
                var raceOverride = team.LiveryOverrides?.FirstOrDefault(o => o.RaceId == raceId);

                string liveryPath = driverNumber == 1
                    ? (raceOverride?.Driver1Livery ?? team.BaseLiveryDriver1)
                    : (raceOverride?.Driver2Livery ?? team.BaseLiveryDriver2);

                if (string.IsNullOrEmpty(liveryPath)) continue;

                string resolved = Path.IsPathRooted(liveryPath)
                    ? liveryPath
                    : Path.Combine(seasonDirectory, liveryPath);

                if (!File.Exists(resolved)) continue;

                try
                {
                    var info = Image.Identify(resolved);
                    return (info.Width, info.Height);
                }
                catch { continue; }
            }

            return null;
        }
    }
}