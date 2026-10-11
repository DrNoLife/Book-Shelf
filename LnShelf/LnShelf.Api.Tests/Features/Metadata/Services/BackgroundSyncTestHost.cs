using System.Collections.Concurrent;
using LnShelf.Api.Features.Notifications.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Just enough of the container for the two background sync runners: a scoped current user,
/// a scoped context over one in-memory database, a sync service that records whom it ran
/// for and fails for whoever it is told to, and a notification pass that records whom it ran
/// for (and can be told to throw, which the real one never does).
/// </summary>
internal sealed class BackgroundSyncTestHost : IAsyncDisposable
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"background-sync-{Guid.CreateVersion7()}";
    private readonly ServiceProvider _provider;

    /// <param name="failingUserId">The user whose run throws, if any.</param>
    /// <param name="databaseIsDown">Resolving the context throws, as a dead database would.</param>
    public BackgroundSyncTestHost(Guid? failingUserId = null, bool databaseIsDown = false)
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => TestUsers.As(null));
        services.AddScoped(provider => databaseIsDown
            ? throw new InvalidOperationException("The database is down")
            : CreateDbContext(provider.GetRequiredService<ICurrentUser>()));

        services.AddScoped(provider =>
        {
            var currentUser = provider.GetRequiredService<ICurrentUser>();
            var sync = Substitute.For<ISeriesSyncService>();

            sync.RunAsync(Arg.Any<LookupBudget>(), Arg.Any<CancellationToken>())
                .Returns(_ => Record(currentUser.Id, failingUserId));
            sync.RunAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Record(currentUser.Id, failingUserId));

            return sync;
        });

        services.AddScoped(provider =>
        {
            var currentUser = provider.GetRequiredService<ICurrentUser>();
            var dispatcher = Substitute.For<IReleaseNotificationDispatcher>();

            dispatcher.DispatchAsync(Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    NotifiedFor.Enqueue(currentUser.Id);
                    return Task.CompletedTask;
                });

            return dispatcher;
        });

        _provider = services.BuildServiceProvider();
    }

    /// <summary>The users a notification pass ran for, in order.</summary>
    public ConcurrentQueue<Guid> NotifiedFor { get; } = new();

    /// <summary>The users a run happened for, in order — including the one that failed.</summary>
    public ConcurrentQueue<Guid> RanFor { get; } = new();

    public SyncRunGate Gate { get; } = new();

    public IServiceScopeFactory ScopeFactory => _provider.GetRequiredService<IServiceScopeFactory>();

    public async Task AddUsersAsync(params Guid[] userIds)
    {
        await using var dbContext = CreateDbContext(TestUsers.As(null));

        // Seen newest first, so the order the night runs them in is the order given here.
        var seenAt = DateTimeOffset.UtcNow;
        foreach (var userId in userIds)
        {
            dbContext.Users.Add(new User
            {
                Id = userId,
                Issuer = "test",
                Subject = userId.ToString(),
                Email = $"{userId}@example.com",
                LastSeenAt = seenAt,
            });
            seenAt = seenAt.AddMinutes(-1);
        }

        await dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private Task<SyncRun> Record(Guid userId, Guid? failingUserId)
    {
        RanFor.Enqueue(userId);

        return userId == failingUserId
            ? throw new InvalidOperationException($"The run for {userId} fell over")
            : Task.FromResult(new SyncRun { Id = Guid.CreateVersion7() });
    }

    private LnShelfDbContext CreateDbContext(ICurrentUser currentUser) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot)
            .Options, currentUser);
}
