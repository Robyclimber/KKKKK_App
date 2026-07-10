using SQLite;

namespace WallPanelPlanner.Persistence;

public interface ISqliteDatabaseFactory
{
    Task<SQLiteAsyncConnection> GetConnectionAsync();

    Task ResetAllDataAsync();
}
