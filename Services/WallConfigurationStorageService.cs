using RouteLab.Models;

namespace RouteLab.Services;

public sealed class WallConfigurationStorageService : IWallConfigurationStorageService
{
    private readonly IWallRepository wallRepository;

    public WallConfigurationStorageService(IWallRepository wallRepository)
    {
        this.wallRepository = wallRepository;
    }

    public async Task<string> SaveAsync(WallDefinition wall, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wall);
        wall.AutoAssignLedIndicesByWallRouting();
        var wallId = await wallRepository.SaveAsync(wall, cancellationToken);
        return $"DB wall id: {wallId}";
    }

    public Task SaveHoleAsync(
        WallDefinition wall,
        WallHoleDefinition hole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wall);
        return wallRepository.SaveHoleAsync(wall, hole, cancellationToken);
    }
}
