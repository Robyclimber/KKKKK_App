using SQLite;
using RouteLab.Persistence.Entities;

namespace RouteLab.Persistence;

public sealed class SqliteDatabaseFactory : ISqliteDatabaseFactory
{
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
