using SQLite;
using RouteLab.Persistence.Entities;

namespace RouteLab.Persistence;

public sealed class SqliteDatabaseFactory : ISqliteDatabaseFactory
{
    private const string CircuitHoleNumberMigrationKey = "routelab.circuit-hole-numbering-v2";
    private const string CircuitColumnNumberMigrationKey = "routelab.circuit-hole-numbering-v3";
    private const string CircuitPanelNumberMigrationKey = "routelab.circuit-hole-numbering-v4";
    private const string CircuitPanelColumnNumberMigrationKey = "routelab.circuit-hole-numbering-v5";
    private const string CircuitPositionNumberMigrationKey = "routelab.circuit-hole-numbering-v6";
    private const string CurrentDatabaseFileName = "ruotelab.db3";
    private const string LegacyDatabaseFileName = "kkkk-konki-kingkong.db3";
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private SQLiteAsyncConnection? connection;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (connection is not null)
        {
            return connection;
        }

        await semaphore.WaitAsync();
        try
        {
            if (connection is not null)
            {
                return connection;
            }

            var dbPath = EnsureDatabasePath();
            connection = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await connection.CreateTableAsync<RoomEntity>();
            await connection.CreateTableAsync<WallEntity>();
            await connection.CreateTableAsync<PanelEntity>();
            await connection.CreateTableAsync<WallHoleEntity>();
            await connection.CreateTableAsync<CircuitEntity>();
            await connection.CreateTableAsync<CircuitMovementEntity>();
            await connection.CreateTableAsync<WorkoutEntity>();
            await connection.CreateTableAsync<WorkoutStepEntity>();
            await EnsureColumnAsync("walls", "RoomName", "TEXT NOT NULL DEFAULT 'Sala Arrampicata'");
            await EnsureColumnAsync("walls", "LedVerticalDirection", "INTEGER NOT NULL DEFAULT 1");
            await EnsureColumnAsync("circuits", "RoomName", "TEXT NOT NULL DEFAULT 'Sala Arrampicata'");
            await EnsureColumnAsync("circuits", "CircuitId", "TEXT NOT NULL DEFAULT ''");
            await EnsureColumnAsync("circuits", "WallNamesJson", "TEXT NOT NULL DEFAULT '[]'");
            await EnsureColumnAsync("circuits", "SuggestNextHoldEnabled", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("circuits", "ClimberProfileId", "TEXT NOT NULL DEFAULT 'default'");
            await EnsureColumnAsync("circuits", "PresetName", "TEXT NOT NULL DEFAULT 'default'");
            await EnsureColumnAsync("circuits", "Effect", "TEXT NOT NULL DEFAULT 'steady'");
            await EnsureColumnAsync("circuits", "DefaultBrightness", "INTEGER NOT NULL DEFAULT 96");
            await EnsureColumnAsync("circuits", "DimmedBrightness", "INTEGER NOT NULL DEFAULT 48");
            await EnsureColumnAsync("circuits", "RightHandColor", "TEXT NOT NULL DEFAULT '#C44536'");
            await EnsureColumnAsync("circuits", "LeftHandColor", "TEXT NOT NULL DEFAULT '#247BA0'");
            await EnsureColumnAsync("circuits", "StartColor", "TEXT NOT NULL DEFAULT '#FFFF00'");
            await EnsureColumnAsync("circuits", "TopColor", "TEXT NOT NULL DEFAULT '#FF0000'");
            await EnsureColumnAsync("circuits", "BlinkCount", "INTEGER NOT NULL DEFAULT 3");
            await EnsureColumnAsync("circuits", "BlinkPeriodMs", "INTEGER NOT NULL DEFAULT 250");
            await EnsureColumnAsync("circuits", "HoldDurationMs", "INTEGER NOT NULL DEFAULT 2500");
            await EnsureColumnAsync("panels", "ImagePath", "TEXT NULL");
            await EnsureColumnAsync("panels", "ImageSourcePath", "TEXT NULL");
            await EnsureColumnAsync("panels", "IsImageRectified", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImageOffsetX", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImageOffsetY", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImageScale", "REAL NOT NULL DEFAULT 1");
            await EnsureColumnAsync("panels", "ImageOpacity", "REAL NOT NULL DEFAULT 0.55");
            await EnsureColumnAsync("panels", "ImageCropLeft", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImageCropTop", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImageCropRight", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImageCropBottom", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImagePerspectiveTopLeftX", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImagePerspectiveTopLeftY", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImagePerspectiveTopRightX", "REAL NOT NULL DEFAULT 1");
            await EnsureColumnAsync("panels", "ImagePerspectiveTopRightY", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImagePerspectiveBottomLeftX", "REAL NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "ImagePerspectiveBottomLeftY", "REAL NOT NULL DEFAULT 1");
            await EnsureColumnAsync("panels", "ImagePerspectiveBottomRightX", "REAL NOT NULL DEFAULT 1");
            await EnsureColumnAsync("panels", "ImagePerspectiveBottomRightY", "REAL NOT NULL DEFAULT 1");
            await EnsureColumnAsync("panels", "LedRoutingAxis", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("panels", "LedStartDirection", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("wall_holes", "HasHold", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("wall_holes", "HoldSize", "INTEGER NOT NULL DEFAULT 2");
            await EnsureColumnAsync("wall_holes", "HoldType", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("wall_holes", "HasEstimatedHoldMetadata", "INTEGER NOT NULL DEFAULT 1");
            await EnsureColumnAsync("wall_holes", "PointId", "TEXT NOT NULL DEFAULT ''");
            await EnsureColumnAsync("wall_holes", "LedIndex", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("wall_holes", "IsEnabled", "INTEGER NOT NULL DEFAULT 1");
            await EnsureDefaultRoomAsync();
            await MigrateCircuitHoleNumbersAsync();
            await MigrateCircuitNumbersToVerticalSerpentineAsync();
            await MigrateCircuitNumbersToPanelOrderAsync();
            await MigrateCircuitNumbersToPanelColumnOrderAsync();
            await MigrateCircuitNumbersToPositionOrderAsync();

            return connection;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task ResetAllDataAsync()
    {
        var sqliteConnection = await GetConnectionAsync();
        await semaphore.WaitAsync();
        try
        {
            await sqliteConnection.RunInTransactionAsync(transaction =>
            {
                transaction.DeleteAll<CircuitMovementEntity>();
                transaction.DeleteAll<CircuitEntity>();
                transaction.DeleteAll<WorkoutStepEntity>();
                transaction.DeleteAll<WorkoutEntity>();
                transaction.DeleteAll<WallHoleEntity>();
                transaction.DeleteAll<PanelEntity>();
                transaction.DeleteAll<WallEntity>();
                transaction.DeleteAll<RoomEntity>();
            });
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task EnsureColumnAsync(string tableName, string columnName, string columnDefinition)
    {
        if (connection is null)
        {
            return;
        }

        var columns = await connection.QueryAsync<PragmaColumnInfo>($"PRAGMA table_info({tableName})");
        if (columns.Any(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await connection.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
    }

    private async Task EnsureDefaultRoomAsync()
    {
        if (connection is null)
        {
            return;
        }

        const string defaultRoomName = "Sala Arrampicata";
        var defaultRoomExists = await connection.Table<RoomEntity>()
            .Where(room => room.Name == defaultRoomName)
            .FirstOrDefaultAsync();

        if (defaultRoomExists is not null)
        {
            return;
        }

        var hasLegacyWalls = await connection.Table<WallEntity>()
            .Where(wall => wall.RoomName == null || wall.RoomName == string.Empty || wall.RoomName == defaultRoomName)
            .FirstOrDefaultAsync();

        var hasLegacyCircuits = await connection.Table<CircuitEntity>()
            .Where(circuit => circuit.RoomName == null || circuit.RoomName == string.Empty || circuit.RoomName == defaultRoomName)
            .FirstOrDefaultAsync();

        if (hasLegacyWalls is null && hasLegacyCircuits is null)
        {
            return;
        }

        await connection.InsertAsync(new RoomEntity
        {
            Name = defaultRoomName
        });
    }

    private async Task MigrateCircuitHoleNumbersAsync()
    {
        if (connection is null || Microsoft.Maui.Storage.Preferences.Default.Get(CircuitHoleNumberMigrationKey, false)) return;

        var circuits = await connection.Table<CircuitEntity>().ToListAsync();
        if (circuits.Count == 0)
        {
            Microsoft.Maui.Storage.Preferences.Default.Set(CircuitHoleNumberMigrationKey, true);
            return;
        }

        var walls = await connection.Table<WallEntity>().ToListAsync();
        var holes = await connection.Table<WallHoleEntity>().ToListAsync();
        var movements = await connection.Table<CircuitMovementEntity>().ToListAsync();
        var changed = new List<CircuitMovementEntity>();

        foreach (var circuit in circuits)
        {
            foreach (var wall in walls.Where(wall => wall.RoomName == circuit.RoomName))
            {
                var wallMovements = movements.Where(movement => movement.CircuitId == circuit.Id && movement.WallName == wall.Name).ToList();
                if (wallMovements.Count == 0) continue;

                var enabled = holes.Where(hole => hole.WallId == wall.Id && hole.IsEnabled).ToList();
                var oldOrder = GetLegacyLedOrder(enabled, wall.LedVerticalDirection).ToList();
                var newOrder = enabled.OrderByDescending(hole => hole.AbsoluteY).ThenBy(hole => hole.AbsoluteX).ThenBy(hole => hole.Id).ToList();
                var newNumbers = newOrder.Select((hole, index) => (hole.Id, number: index + 1)).ToDictionary(item => item.Id, item => item.number);

                foreach (var movement in wallMovements)
                {
                    if (movement.HoleNumber <= 0 || movement.HoleNumber > oldOrder.Count) continue;
                    var replacement = newNumbers[oldOrder[movement.HoleNumber - 1].Id];
                    if (replacement == movement.HoleNumber) continue;
                    movement.HoleNumber = replacement;
                    changed.Add(movement);
                }
            }
        }

        if (changed.Count > 0)
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                foreach (var movement in changed) transaction.Update(movement);
            });
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(CircuitHoleNumberMigrationKey, true);
    }

    private static IEnumerable<WallHoleEntity> GetLegacyLedOrder(IEnumerable<WallHoleEntity> holes, int direction)
    {
        const double tolerance = 0.0001d;
        var columns = holes.GroupBy(hole => Math.Round(hole.AbsoluteX / tolerance) * tolerance).OrderBy(group => group.Key).ToList();
        var firstTopToBottom = direction != 0;
        for (var index = 0; index < columns.Count; index++)
        {
            var topToBottom = index % 2 == 0 ? firstTopToBottom : !firstTopToBottom;
            foreach (var hole in topToBottom
                         ? columns[index].OrderBy(hole => hole.AbsoluteY).ThenBy(hole => hole.Id)
                         : columns[index].OrderByDescending(hole => hole.AbsoluteY).ThenBy(hole => hole.Id))
            {
                yield return hole;
            }
        }
    }

    private async Task MigrateCircuitNumbersToVerticalSerpentineAsync()
    {
        if (connection is null || Microsoft.Maui.Storage.Preferences.Default.Get(CircuitColumnNumberMigrationKey, false)) return;

        var circuits = await connection.Table<CircuitEntity>().ToListAsync();
        var walls = await connection.Table<WallEntity>().ToListAsync();
        var holes = await connection.Table<WallHoleEntity>().ToListAsync();
        var movements = await connection.Table<CircuitMovementEntity>().ToListAsync();
        var changed = new List<CircuitMovementEntity>();

        foreach (var circuit in circuits)
        {
            foreach (var wall in walls.Where(wall => wall.RoomName == circuit.RoomName))
            {
                var wallMovements = movements.Where(movement => movement.CircuitId == circuit.Id && movement.WallName == wall.Name).ToList();
                var wallHoles = holes.Where(hole => hole.WallId == wall.Id).ToList();
                var previousOrder = GetBottomToTopRowsOrder(wallHoles).ToList();
                var newOrder = GetVerticalSerpentineOrder(wallHoles).ToList();
                var newNumbers = newOrder.Select((hole, index) => (hole.Id, number: index + 1)).ToDictionary(item => item.Id, item => item.number);

                foreach (var movement in wallMovements)
                {
                    if (movement.HoleNumber <= 0 || movement.HoleNumber > previousOrder.Count) continue;
                    var replacement = newNumbers[previousOrder[movement.HoleNumber - 1].Id];
                    if (replacement == movement.HoleNumber) continue;
                    movement.HoleNumber = replacement;
                    changed.Add(movement);
                }
            }
        }

        if (changed.Count > 0)
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                foreach (var movement in changed) transaction.Update(movement);
            });
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(CircuitColumnNumberMigrationKey, true);
    }

    private static IEnumerable<WallHoleEntity> GetBottomToTopRowsOrder(IEnumerable<WallHoleEntity> holes)
    {
        const double tolerance = 0.0001d;
        return holes
            .GroupBy(hole => Math.Round(hole.AbsoluteY / tolerance) * tolerance)
            .OrderByDescending(group => group.Key)
            .SelectMany(group => group.OrderBy(hole => hole.AbsoluteX).ThenBy(hole => hole.Id));
    }

    private static IEnumerable<WallHoleEntity> GetVerticalSerpentineOrder(IEnumerable<WallHoleEntity> holes)
    {
        const double tolerance = 0.0001d;
        var columns = holes
            .GroupBy(hole => Math.Round(hole.AbsoluteX / tolerance) * tolerance)
            .OrderBy(group => group.Key)
            .ToList();

        return columns.SelectMany((column, index) =>
            index % 2 == 0
                ? column.OrderByDescending(hole => hole.AbsoluteY).ThenBy(hole => hole.Id)
                : column.OrderBy(hole => hole.AbsoluteY).ThenBy(hole => hole.Id));
    }

    private async Task MigrateCircuitNumbersToPanelOrderAsync()
    {
        if (connection is null || Microsoft.Maui.Storage.Preferences.Default.Get(CircuitPanelNumberMigrationKey, false)) return;
        var walls = await connection.Table<WallEntity>().ToListAsync();
        var holes = await connection.Table<WallHoleEntity>().ToListAsync();
        var movements = await connection.Table<CircuitMovementEntity>().ToListAsync();
        var changed = new List<CircuitMovementEntity>();

        foreach (var wall in walls)
        {
            var wallHoles = holes.Where(hole => hole.WallId == wall.Id).ToList();
            var oldOrder = GetVerticalSerpentineOrder(wallHoles).ToList();
            var newOrder = GetPanelNameOrder(wallHoles).ToList();
            var newNumbers = newOrder.Select((hole, index) => (hole.Id, index: index + 1)).ToDictionary(item => item.Id, item => item.index);
            foreach (var movement in movements.Where(movement => movement.WallName == wall.Name && movement.HoleNumber > 0 && movement.HoleNumber <= oldOrder.Count))
            {
                var replacement = newNumbers[oldOrder[movement.HoleNumber - 1].Id];
                if (replacement == movement.HoleNumber) continue;
                movement.HoleNumber = replacement;
                changed.Add(movement);
            }
        }

        if (changed.Count > 0) await connection.RunInTransactionAsync(transaction => { foreach (var movement in changed) transaction.Update(movement); });
        Microsoft.Maui.Storage.Preferences.Default.Set(CircuitPanelNumberMigrationKey, true);
    }

    private static IEnumerable<WallHoleEntity> GetPanelNameOrder(IEnumerable<WallHoleEntity> holes)
    {
        const double tolerance = 0.0001d;
        return holes.GroupBy(hole => hole.PanelName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => (GetPanelOrdinal(group.Key) - 1) / 6)
            .ThenByDescending(group => GetPanelOrdinal(group.Key))
            .SelectMany(group => group.GroupBy(hole => Math.Round(hole.RelativeX / tolerance) * tolerance).OrderBy(column => column.Key)
                .SelectMany((column, index) => index % 2 == 0 ? column.OrderByDescending(hole => hole.RelativeY).ThenBy(hole => hole.Id) : column.OrderBy(hole => hole.RelativeY).ThenBy(hole => hole.Id)));
    }

    private async Task MigrateCircuitNumbersToPanelColumnOrderAsync()
    {
        if (connection is null || Microsoft.Maui.Storage.Preferences.Default.Get(CircuitPanelColumnNumberMigrationKey, false)) return;

        var walls = await connection.Table<WallEntity>().ToListAsync();
        var holes = await connection.Table<WallHoleEntity>().ToListAsync();
        var movements = await connection.Table<CircuitMovementEntity>().ToListAsync();
        var changed = new List<CircuitMovementEntity>();

        foreach (var wall in walls)
        {
            var wallHoles = holes.Where(hole => hole.WallId == wall.Id).ToList();
            var oldOrder = GetSharedRelativeXPanelColumnOrder(wallHoles).ToList();
            var newOrder = GetOrdinalPanelColumnOrder(wallHoles).ToList();
            var newNumbers = newOrder.Select((hole, index) => (hole.Id, index: index + 1)).ToDictionary(item => item.Id, item => item.index);

            foreach (var movement in movements.Where(movement => movement.WallName == wall.Name && movement.HoleNumber > 0 && movement.HoleNumber <= oldOrder.Count))
            {
                var replacement = newNumbers[oldOrder[movement.HoleNumber - 1].Id];
                if (replacement == movement.HoleNumber) continue;
                movement.HoleNumber = replacement;
                changed.Add(movement);
            }
        }

        if (changed.Count > 0) await connection.RunInTransactionAsync(transaction => { foreach (var movement in changed) transaction.Update(movement); });
        Microsoft.Maui.Storage.Preferences.Default.Set(CircuitPanelColumnNumberMigrationKey, true);
    }

    private static IEnumerable<WallHoleEntity> GetSharedRelativeXPanelColumnOrder(IEnumerable<WallHoleEntity> holes)
    {
        return holes.GroupBy(hole => hole.PanelName, StringComparer.OrdinalIgnoreCase)
            .GroupBy(panel => (GetPanelOrdinal(panel.Key) - 1) / 6)
            .OrderBy(column => column.Key)
            .SelectMany(wallColumn =>
            {
                var panels = wallColumn.ToList();
                var columns = panels.SelectMany(panel => panel.Select(hole => Math.Round(hole.RelativeX, 4))).Distinct().OrderBy(x => x).ToList();
                return columns.SelectMany((x, index) =>
                {
                    var bottomToTop = index % 2 == 0;
                    var orderedPanels = bottomToTop ? panels.OrderByDescending(panel => GetPanelOrdinal(panel.Key)) : panels.OrderBy(panel => GetPanelOrdinal(panel.Key));
                    return orderedPanels.SelectMany(panel =>
                    {
                        var panelHoles = panel.Where(hole => Math.Abs(Math.Round(hole.RelativeX, 4) - x) < 0.0001d);
                        return bottomToTop ? panelHoles.OrderByDescending(hole => hole.RelativeY) : panelHoles.OrderBy(hole => hole.RelativeY);
                    });
                });
            });
    }

    private static IEnumerable<WallHoleEntity> GetOrdinalPanelColumnOrder(IEnumerable<WallHoleEntity> holes)
    {
        return holes.GroupBy(hole => hole.PanelName, StringComparer.OrdinalIgnoreCase)
            .GroupBy(panel => (GetPanelOrdinal(panel.Key) - 1) / 6)
            .OrderBy(column => column.Key)
            .SelectMany(wallColumn =>
            {
                var panels = wallColumn.ToList();
                var panelColumns = panels.ToDictionary(panel => panel.Key, panel => panel.GroupBy(hole => Math.Round(hole.RelativeX, 4)).OrderBy(column => column.Key).Select(column => column.ToList()).ToList(), StringComparer.OrdinalIgnoreCase);
                var count = panelColumns.Values.Max(columns => columns.Count);
                return Enumerable.Range(0, count).SelectMany(index =>
                {
                    var bottomToTop = index % 2 == 0;
                    var orderedPanels = bottomToTop ? panels.OrderByDescending(panel => GetPanelOrdinal(panel.Key)) : panels.OrderBy(panel => GetPanelOrdinal(panel.Key));
                    return orderedPanels.SelectMany(panel =>
                    {
                        var columns = panelColumns[panel.Key];
                        if (index >= columns.Count) return Enumerable.Empty<WallHoleEntity>();
                        return bottomToTop ? columns[index].OrderByDescending(hole => hole.RelativeY) : columns[index].OrderBy(hole => hole.RelativeY);
                    });
                });
            });
    }

    private async Task MigrateCircuitNumbersToPositionOrderAsync()
    {
        if (connection is null || Microsoft.Maui.Storage.Preferences.Default.Get(CircuitPositionNumberMigrationKey, false)) return;

        var walls = await connection.Table<WallEntity>().ToListAsync();
        var holes = await connection.Table<WallHoleEntity>().ToListAsync();
        var movements = await connection.Table<CircuitMovementEntity>().ToListAsync();
        var changed = new List<CircuitMovementEntity>();

        foreach (var wall in walls)
        {
            var wallHoles = holes.Where(hole => hole.WallId == wall.Id).ToList();
            var oldOrder = GetOrdinalPanelColumnOrder(wallHoles).ToList();
            var newOrder = GetVerticalSerpentineOrder(wallHoles).ToList();
            var newNumbers = newOrder.Select((hole, index) => (hole.Id, index: index + 1)).ToDictionary(item => item.Id, item => item.index);

            foreach (var movement in movements.Where(movement => movement.WallName == wall.Name && movement.HoleNumber > 0 && movement.HoleNumber <= oldOrder.Count))
            {
                var replacement = newNumbers[oldOrder[movement.HoleNumber - 1].Id];
                if (replacement == movement.HoleNumber) continue;
                movement.HoleNumber = replacement;
                changed.Add(movement);
            }
        }

        if (changed.Count > 0) await connection.RunInTransactionAsync(transaction => { foreach (var movement in changed) transaction.Update(movement); });
        Microsoft.Maui.Storage.Preferences.Default.Set(CircuitPositionNumberMigrationKey, true);
    }

    private static int GetPanelOrdinal(string panelName)
    {
        var digits = new string(panelName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var ordinal) ? ordinal : int.MaxValue;
    }

    private static string EnsureDatabasePath()
    {
        var currentPath = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, CurrentDatabaseFileName);
        if (System.IO.File.Exists(currentPath))
        {
            return currentPath;
        }

        var legacyPath = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, LegacyDatabaseFileName);
        if (System.IO.File.Exists(legacyPath))
        {
            System.IO.File.Copy(legacyPath, currentPath, overwrite: false);
        }

        return currentPath;
    }

    private sealed class PragmaColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}
