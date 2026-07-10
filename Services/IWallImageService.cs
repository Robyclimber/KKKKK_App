namespace WallPanelPlanner.Services;

public interface IWallImageService
{
    Task<string?> PickAndImportImageAsync(CancellationToken cancellationToken = default);
}
