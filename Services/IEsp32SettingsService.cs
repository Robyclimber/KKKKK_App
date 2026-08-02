using RouteLab.Models;

namespace RouteLab.Services;

public interface IEsp32SettingsService
{
    Esp32DeviceSettings Load();

    void Save(Esp32DeviceSettings settings);
}

