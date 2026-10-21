using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;

namespace Ams2ChEd.Business.AMS2.GameLogic
{
    /// <summary>
    /// The livery preview AMS2 shows in its car selection screen (the team's LiveryPreview, or the
    /// race's livery override's), read from the season pack folder the race files are generated
    /// from.
    /// </summary>
    public class Ams2TeamPreviewProvider : ITeamPreviewProvider
    {
        public byte[] LoadTeamPreviewPng(ISeason season, string teamId, int raceId, int maxWidth)
        {
            if (season.Teams.FirstOrDefault(t => t.TeamId == teamId) is not Ams2TeamEntry team)
                return null;

            var previewFile = team.LiveryOverrides?.FirstOrDefault(o => o.RaceId == raceId)?.LiveryPreview ?? team.LiveryPreview;
            if (string.IsNullOrEmpty(previewFile))
                return null;

            var seasonDirectory = Path.GetDirectoryName(StoragePaths.SeasonFilePath(season.OriginalYear ?? season.Year));
            var previewPath = Path.IsPathRooted(previewFile) ? previewFile : Path.Combine(seasonDirectory, previewFile);
            if (!File.Exists(previewPath))
                return null;

            try
            {
                return DdsTextureComposer.LoadAsPng(previewPath, maxWidth);
            }
            catch (Exception ex)
            {
                // a picture that can't be decoded only costs the tile its picture
                Console.WriteLine($"Warning: livery preview could not be read: {previewPath} ({ex.Message})");
                return null;
            }
        }
    }
}
