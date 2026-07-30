using Microsoft.Maui.Storage;
using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class Esp32SettingsService : IEsp32SettingsService
{
    private const string BaseUrlKey = "esp32.baseUrl";
    private const string ControllerIdKey = "esp32.controllerId";
    private const string WallLedCountKey = "esp32.wallLedCount";
    private const string BrightnessLimitKey = "esp32.brightnessLimit";

    public Esp32DeviceSettings Load()
    {
        return new Esp32DeviceSettings
        {
            BaseUrl = Preferences.Default.Get(BaseUrlKey, "http://192.168.1.50"),
            ControllerId = Preferences.Default.Get(ControllerIdKey, "esp32-sala-1"),
            WallLedCount = Preferences.Default.Get(WallLedCountKey, 240),
            BrightnessLimit = Preferences.Default.Get(BrightnessLimitKey, 128)
        };
    }

    public void Save(Esp32DeviceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Preferences.Default.Set(BaseUrlKey, settings.BaseUrl);
        Preferences.Default.Set(ControllerIdKey, settings.ControllerId);
        Preferences.Default.Set(WallLedCountKey, settings.WallLedCount);
        Preferences.Default.Set(BrightnessLimitKey, settings.BrightnessLimit);
    }
}
