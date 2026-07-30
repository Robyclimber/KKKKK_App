using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IAppSettingsService
{
    AppSettingsDefinition Load();

    void Save(AppSettingsDefinition settings);
}
