namespace WallPanelPlanner.Services;

public interface IWallConfigurationStorageService
{
    Task<string> SaveAsync(Models.WallDefinition wall, CancellationToken cancellationToken = default);
}
