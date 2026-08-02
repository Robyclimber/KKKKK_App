using Microsoft.Maui.Storage;
using RouteLab.Models;

namespace RouteLab.Services;

public sealed class Esp32SettingsService : IEsp32SettingsService
{
    private const string BaseUrlKey = "esp32.baseUrl";
    private const string ControllerIdKey = "esp32.controllerId";
    private const string WallLedCountKey = "esp32.wallLedCount";
    private const string BrightnessLimitKey = "esp32.brightnessLimit";
    private readonly IBusyIndicatorService? busyIndicatorService;

    public Esp32SettingsService(IBusyIndicatorService? busyIndicatorService = null)
    {
        this.busyIndicatorService = busyIndicatorService;
    }

    public Esp32DeviceSettings Load()
    {
        return Execute("Caricamento impostazioni scheda...", () => new Esp32DeviceSettings
        {
            BaseUrl = Preferences.Default.Get(BaseUrlKey, "http://192.168.1.50"),
            ControllerId = Preferences.Default.Get(ControllerIdKey, "routelab-hub-sala-1"),
            WallLedCount = Preferences.Default.Get(WallLedCountKey, 240),
            BrightnessLimit = Preferences.Default.Get(BrightnessLimitKey, 128)
        });
    }

    public void Save(Esp32DeviceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Execute("Salvataggio impostazioni scheda...", () =>
        {
            Preferences.Default.Set(BaseUrlKey, settings.BaseUrl);
            Preferences.Default.Set(ControllerIdKey, settings.ControllerId);
            Preferences.Default.Set(WallLedCountKey, settings.WallLedCount);
            Preferences.Default.Set(BrightnessLimitKey, settings.BrightnessLimit);
        });
    }

    private T Execute<T>(string message, Func<T> action)
    {
        return busyIndicatorService is null
            ? action()
            : busyIndicatorService.Run(message, action);
    }

    private void Execute(string message, Action action)
    {
        if (busyIndicatorService is null)
        {
            action();
            return;
        }

        busyIndicatorService.Run(message, action);
    }
}
