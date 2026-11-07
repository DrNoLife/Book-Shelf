using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace LnShelf.Api.Tests.Data.Postgres;

/// <summary>
/// One throwaway Postgres for every test in the <see cref="PostgresCollection"/>, with the
/// real migrations applied once, the way the API applies them on boot.
/// </summary>
// Never emptied between tests: each test signs up its own user and writes only that
// user's shelf, so tests cannot see each other's rows through the owner filters and a
// fresh user costs one insert instead of a schema reset.
public sealed class PostgresDatabase : IAsyncLifetime
{
    // The image docker-compose.yml pins, so these tests run against what production runs.
    private const string Image = "postgres:17-alpine";

    private PostgreSqlContainer? _container;

    public async Task InitializeAsync()
    {
        // Building the container already asks for Docker, so a machine without it builds
        // nothing; every test in the collection is skipped there anyway (PostgresFact).
        if (DockerAvailability.SkipReason is not null)
        {
            return;
        }

        _container = new PostgreSqlBuilder(Image).Build();
        await _container.StartAsync();

        await using var dbContext = CreateContext(userId: null);
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>A context acting for <paramref name="userId"/>, as a request scope would.</summary>
    public LnShelfDbContext CreateContext(Guid? userId, params IInterceptor[] interceptors)
    {
        var container = _container
            ?? throw new InvalidOperationException("The Postgres container did not start; is Docker running?");

        var options = new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .AddInterceptors(interceptors)
            .Options;

        return new LnShelfDbContext(options, TestUsers.As(userId));
    }

    /// <summary>Signs up a new user, whose shelf starts empty.</summary>
    public async Task<Guid> CreateUserAsync()
    {
        var userId = Guid.CreateVersion7();

        await using var dbContext = CreateContext(userId);
        dbContext.Users.Add(new User
        {
            Id = userId,
            Issuer = "https://accounts.google.com",
            Subject = userId.ToString(),
            Email = $"{userId}@example.com",
            CreatedAt = DateTimeOffset.UnixEpoch,
            LastSeenAt = DateTimeOffset.UnixEpoch,
        });
        await dbContext.SaveChangesAsync();

        return userId;
    }

    /// <summary>A series on <paramref name="userId"/>'s shelf with untouched volumes 1..N.</summary>
    // The series has its user-state row, as every add path writes one; the volumes have none,
    // which is the untouched state the first-touch writes start from.
    public async Task<Series> CreateSeriesAsync(Guid userId, int volumeCount, int aniListId = 94846)
    {
        await using var dbContext = CreateContext(userId);

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = "The Saga of Tanya the Evil",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>The tests that share <see cref="PostgresDatabase"/>, run one at a time.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "Postgres";
}
