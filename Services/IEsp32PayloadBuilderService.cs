using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IEsp32PayloadBuilderService
{
    Esp32WallConfigPayload BuildWallConfig(WallDefinition wall, RoomDefinition room, Esp32DeviceSettings settings);

    Esp32CircuitsPayload BuildCircuitsPayload(WallDefinition wall, RoomDefinition room, IEnumerable<CircuitDefinition> circuits);
}
