using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// A throwaway Postgres for the run, the same image docker-compose.yml pins, emptied
/// between tests by Respawn so each journey starts from the seeded shelf.
/// </summary>
public sealed class PostgresDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();

    /// <summary>Deletes every row except the migration history.</summary>
    // The respawner is created lazily because it reads the schema, which only exists once
    // the host has started and run its migrations.
    public async Task EmptyAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });

        await _respawner.ResetAsync(connection);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
