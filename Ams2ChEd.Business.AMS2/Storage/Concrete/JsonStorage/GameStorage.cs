using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class GameStorage : IGameStorage
    {
        public string SaveGame(ISaveGame saveGame, string saveName)
        {
            try
            {
                saveGame.Timestamp = DateTime.Now;
                // Create saves directory if it doesn't exist
                if (!Directory.Exists(StoragePaths.SavesFolder))
                {
                    Directory.CreateDirectory(StoragePaths.SavesFolder);
                }

                // Generate filename
                string fileName = $"{saveName}.json";
                string fullPath = Path.Combine(StoragePaths.SavesFolder, fileName);

                string json = JsonSerializer.Serialize(saveGame, DefaultJsonSerializerOptions.Instance);
                File.WriteAllText(fullPath, json);

                return fullPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving game: {ex.Message}", ex);
            }
        }

        public ISaveGame LoadGame(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Save file not found: {filePath}");
                }

                string json = File.ReadAllText(filePath);

                var saveGame = JsonSerializer.Deserialize<SaveGame>(json, DefaultJsonSerializerOptions.Instance);
                return saveGame;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading game: {ex.Message}", ex);
            }
        }

        public string[] GetSaveFiles()
        {
            try
            {
                if (!Directory.Exists(StoragePaths.SavesFolder))
                {
                    return Array.Empty<string>();
                }

                return Directory.GetFiles(StoragePaths.SavesFolder, "*.json");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting save files: {ex.Message}", ex);
            }
        }

        public void DeleteSave(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting save file: {ex.Message}", ex);
            }
        }
    }
}