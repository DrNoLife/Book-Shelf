namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// One night's run: every user gets theirs, and nothing that goes wrong at 04:00 escapes to
/// stop the host.
/// </summary>
// Worth testing because the failure is silent and total: an exception out of a
// BackgroundService stops the whole app, and nobody is awake to see it happen.
public sealed class NightlySyncBackgroundServiceRunTests
{
    private static readonly Guid First = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Second = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Third = new("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public async Task RunOnceAsync_OneUsersRunFails_TheOthersStillRun()
    {
        await using var host = new BackgroundSyncTestHost(failingUserId: Second);
        await host.AddUsersAsync(First, Second, Third);

        await CreateService(host).RunOnceAsync(CancellationToken.None);

        host.RanFor.ShouldBe([First, Second, Third]);
    }

    // After each user's run, failed or not, and as that user: a run that fell over halfway
    // saved the series before it, and their news is still news.
    [Fact]
    public async Task RunOnceAsync_EveryUser_GetsANotificationPassAfterTheirRun()
    {
        await using var host = new BackgroundSyncTestHost(failingUserId: Second);
        await host.AddUsersAsync(First, Second, Third);

        await CreateService(host).RunOnceAsync(CancellationToken.None);

        host.NotifiedFor.ShouldBe([First, Second, Third]);
    }

    [Fact]
    public async Task RunOnceAsync_EveryRunCompletes_BeatsUpWithTheCount()
    {
        await using var host = new BackgroundSyncTestHost();
        await host.AddUsersAsync(First, Second);
        var heartbeat = Substitute.For<IHeartbeat>();

        await CreateService(host, heartbeat).RunOnceAsync(CancellationToken.None);

        await heartbeat.Received(1).BeatAsync(
            HeartbeatMonitors.NightlySync, HeartbeatOutcome.Up, "2 shelves synced, 0 failed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_OneUsersRunFails_BeatsDown()
    {
        await using var host = new BackgroundSyncTestHost(failingUserId: Second);
        await host.AddUsersAsync(First, Second, Third);
        var heartbeat = Substitute.For<IHeartbeat>();

        await CreateService(host, heartbeat).RunOnceAsync(CancellationToken.None);

        await heartbeat.Received(1).BeatAsync(
            HeartbeatMonitors.NightlySync, HeartbeatOutcome.Down, "2 shelves synced, 1 failed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_TheUserQueryFails_BeatsDown()
    {
        await using var host = new BackgroundSyncTestHost(databaseIsDown: true);
        var heartbeat = Substitute.For<IHeartbeat>();

        await CreateService(host, heartbeat).RunOnceAsync(CancellationToken.None);

        await heartbeat.Received(1).BeatAsync(
            HeartbeatMonitors.NightlySync, HeartbeatOutcome.Down, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_TheUserQueryFails_DoesNotThrow()
    {
        // The database blinking at 04:00: before, this escaped ExecuteAsync and .NET stopped
        // the host over it.
        await using var host = new BackgroundSyncTestHost(databaseIsDown: true);

        await Should.NotThrowAsync(() => CreateService(host).RunOnceAsync(CancellationToken.None));

        host.RanFor.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunOnceAsync_AUserWhoseSyncIsAlreadyRunning_IsSkipped()
    {
        await using var host = new BackgroundSyncTestHost();
        await host.AddUsersAsync(First, Second);

        // A "Sync now" still going for the first user is doing tonight's work for that shelf.
        using var manualRun = host.Gate.TryEnter(First);

        await CreateService(host).RunOnceAsync(CancellationToken.None);

        host.RanFor.ShouldBe([Second]);
    }

    private static NightlySyncBackgroundService CreateService(BackgroundSyncTestHost host, IHeartbeat? heartbeat = null) =>
        new(
            host.ScopeFactory,
            host.Gate,
            Options.Create(new SyncOptions { Enabled = true }),
            Options.Create(new OpenLibraryOptions()),
            TimeProvider.System,
            heartbeat ?? Substitute.For<IHeartbeat>(),
            NullLogger<NightlySyncBackgroundService>.Instance);
}
