namespace RouteLab.Services;

public interface IWallConfigurationStorageService
{
    Task<string> SaveAsync(Models.WallDefinition wall, CancellationToken cancellationToken = default);

    Task SaveHoleAsync(
        Models.WallDefinition wall,
        Models.WallHoleDefinition hole,
        CancellationToken cancellationToken = default);
}
