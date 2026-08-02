using RouteLab.Models;

namespace RouteLab.Services;

public interface IAppSettingsService
{
    AppSettingsDefinition Load();

    void Save(AppSettingsDefinition settings);
}

