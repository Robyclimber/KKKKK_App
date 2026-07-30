namespace WallPanelPlanner.Models;

public sealed class Esp32DeviceSettings
{
    public string BaseUrl { get; set; } = "http://192.168.1.50";

    public string ControllerId { get; set; } = "esp32-sala-1";

    public int WallLedCount { get; set; } = 240;

    public int BrightnessLimit { get; set; } = 128;
}
