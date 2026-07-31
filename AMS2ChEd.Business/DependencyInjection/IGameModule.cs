using Microsoft.Extensions.DependencyInjection;

namespace AMS2ChEd.Business.DependencyInjection
{
    public class GameModuleStartupOptions
    {
        public bool ScenarioCreatorMode { get; init; }

        public bool DeveloperMode { get; init; }
    }

    /// <summary>
    /// Compile-time seam for "which game" - the composition root (App.xaml.cs) constructs exactly
    /// one IGameModule implementation and delegates all game-specific DI registration to it.
    /// </summary>
    public interface IGameModule
    {
        string GameId { get; }

        string DisplayName { get; }

        /// <summary>
        /// Bare filename (not a path) of this game's downloadable-seasons catalog, resolved via
        /// AppPaths.SeasonsManifestPath(fileName). Lets each game ship its own manifest content.
        /// </summary>
        string SeasonsManifestFileName { get; }

        void RegisterServices(IServiceCollection services, GameModuleStartupOptions options);
    }
}
