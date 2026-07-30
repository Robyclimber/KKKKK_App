using RuoteLab.Models;

namespace RuoteLab.Services;

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
        wall.ValidateHardwareMappings();
        var wallId = await wallRepository.SaveAsync(wall, cancellationToken);
        return $"DB wall id: {wallId}";
    }
}
