using LiveWallpaper.App.Services;
using LiveWallpaper.Core;
using LiveWallpaper.Players;

namespace LiveWallpaper.App;

public sealed class WallpaperOrchestratorService : IDisposable
{
    public WallpaperOrchestrator Orchestrator { get; }

    public WallpaperOrchestratorService()
    {
        Orchestrator = new WallpaperOrchestrator(new WallpaperPlayerFactory());
    }

    public Task InitializeAsync() => Orchestrator.InitializeAsync();

    public Task ShutdownAsync() => Orchestrator.ShutdownAsync();

    public void Dispose() => Orchestrator.Dispose();
}
