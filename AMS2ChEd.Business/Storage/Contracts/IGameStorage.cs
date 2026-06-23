using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IGameStorage
    {
        string SaveGame(ISaveGame saveGame, string saveName);

        ISaveGame LoadGame(string filePath);

        string[] GetSaveFiles();

        void DeleteSave(string filePath);
    }
}
