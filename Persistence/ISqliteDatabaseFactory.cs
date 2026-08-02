using SQLite;

namespace RouteLab.Persistence;

public interface ISqliteDatabaseFactory
{
    Task<SQLiteAsyncConnection> GetConnectionAsync();

    Task ResetAllDataAsync();
}

