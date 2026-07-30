using SQLite;

namespace RuoteLab.Persistence;

public interface ISqliteDatabaseFactory
{
    Task<SQLiteAsyncConnection> GetConnectionAsync();

    Task ResetAllDataAsync();
}
