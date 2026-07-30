using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IEsp32SettingsService
{
    Esp32DeviceSettings Load();

    void Save(Esp32DeviceSettings settings);
}
