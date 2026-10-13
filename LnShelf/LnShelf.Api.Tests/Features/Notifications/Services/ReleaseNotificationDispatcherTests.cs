using LnShelf.Api.Features.Notifications.Services;
using LnShelf.Api.Features.Releases.Abstractions;
using LnShelf.Api.Features.Releases.Models;
using LnShelf.Api.Tests.Features.Notifications.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Notifications.Services;

/// <summary>
/// The pass after a run: one digest of what is pending, the outbox marked, muted and stale
/// events settled without a push, and the log pruned. Never fails the run it follows.
/// </summary>
public sealed class ReleaseNotificationDispatcherTests : IDisposable
{
    private readonly NotificationsHarness _harness = new();
    private readonly IReleaseNotifier _notifier = Substitute.For<IReleaseNotifier>();

    public ReleaseNotificationDispatcherTests()
    {
        _notifier.IsAvailable.Returns(true);
        Delivers(new NotificationDelivery(Devices: 1, Delivered: 1, FailedTransiently: 0, Removed: 0));
    }

    private DateTimeOffset Now => _harness.Time.GetUtcNow();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task DispatchAsync_PendingEvents_AreSentAsOneDigestAndMarked()
    {
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(series, 13, Now.AddHours(-1)), Event(series, 14, Now.AddHours(-1)));

        await DispatchAsync();

        await _notifier.Received(1).NotifyAsync(
            Arg.Is<ReleaseNotification>(notification => notification.Title == "2 new volumes out"),
            Arg.Any<CancellationToken>());
        (await PendingAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_TheSecondPass_SendsNothingAgain()
    {
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(series, 14, Now.AddHours(-1)));

        await DispatchAsync();
        await DispatchAsync();

        await _notifier.Received(1).NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_AMutedSeries_IsSettledWithoutAPush()
    {
        var muted = await AddSeriesAsync("Overlord", muted: true);
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(muted, 17, Now.AddHours(-1)), Event(series, 14, Now.AddHours(-1)));

        await DispatchAsync();

        // Only the unmuted series is in the digest; the muted one's event is still marked, so
        // unmuting later does not push old news.
        await _notifier.Received(1).NotifyAsync(
            Arg.Is<ReleaseNotification>(notification => notification.Body == "86 Vol 14"),
            Arg.Any<CancellationToken>());
        (await PendingAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_OnlyStaleEvents_AreSettledWithoutAPush()
    {
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(series, 14, Now - ReleaseNotificationDispatcher.StaleAfter - TimeSpan.FromHours(1)));

        await DispatchAsync();

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
        (await PendingAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_EveryDeviceFailedTransiently_LeavesTheEventsForTheNextPass()
    {
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(series, 14, Now.AddHours(-1)));
        Delivers(new NotificationDelivery(Devices: 2, Delivered: 0, FailedTransiently: 2, Removed: 0));

        await DispatchAsync();

        (await PendingAsync()).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 0, 1)]
    public async Task DispatchAsync_NothingLeftToRetry_MarksTheEvents(int devices, int transient, int removed)
    {
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(series, 14, Now.AddHours(-1)));
        Delivers(new NotificationDelivery(devices, Delivered: 0, transient, removed));

        await DispatchAsync();

        // No device, a refusal that would only repeat, or a device that is gone: the events
        // are dealt with, and a later device does not get a burst of old news.
        (await PendingAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_TheNotifierThrows_DoesNotThrowAndKeepsTheEvents()
    {
        var series = await AddSeriesAsync("86");
        await AddEventsAsync(Event(series, 14, Now.AddHours(-1)));
        _notifier.NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>())
            .Returns<NotificationDelivery>(_ => throw new InvalidOperationException("push service on fire"));

        await Should.NotThrowAsync(DispatchAsync);

        (await PendingAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DispatchAsync_EventsPastTheRetention_ArePruned()
    {
        var series = await AddSeriesAsync("86");
        var old = Event(series, 1, Now - ReleaseNotificationDispatcher.KeepFor - TimeSpan.FromDays(1));
        old.NotifiedAt = old.OccurredAt;
        var recent = Event(series, 2, Now - TimeSpan.FromDays(30));
        recent.NotifiedAt = recent.OccurredAt;
        await AddEventsAsync(old, recent);

        await DispatchAsync();

        await using var dbContext = _harness.CreateDbContext();
        (await dbContext.ShelfEvents.SingleAsync()).Id.ShouldBe(recent.Id);
    }

    // The retention the privacy page states must not hang on the push path working.
    [Fact]
    public async Task DispatchAsync_TheNotifierThrows_StillPrunesAndKeepsTheRecentEventPending()
    {
        var series = await AddSeriesAsync("86");
        var old = Event(series, 1, Now - ReleaseNotificationDispatcher.KeepFor - TimeSpan.FromDays(1));
        old.NotifiedAt = old.OccurredAt;
        var recent = Event(series, 2, Now.AddHours(-1));
        await AddEventsAsync(old, recent);
        _notifier.NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>())
            .Returns<NotificationDelivery>(_ => throw new InvalidOperationException("push service on fire"));

        await Should.NotThrowAsync(DispatchAsync);

        await using var dbContext = _harness.CreateDbContext();
        var left = await dbContext.ShelfEvents.SingleAsync();
        left.Id.ShouldBe(recent.Id);
        left.NotifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_AnotherUsersEvents_AreLeftAlone()
    {
        Guid otherSeriesId;
        await using (var someoneElse = _harness.CreateDbContext(TestUsers.SomeoneElse))
        {
            var other = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "Not mine" };
            someoneElse.Series.Add(other);
            someoneElse.ShelfEvents.Add(Event(other, 3, Now.AddHours(-1)));
            await someoneElse.SaveChangesAsync();
            otherSeriesId = other.Id;
        }

        await DispatchAsync();

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
        await using var check = _harness.CreateDbContext(TestUsers.SomeoneElse);
        (await check.ShelfEvents.SingleAsync(shelfEvent => shelfEvent.SeriesId == otherSeriesId)).NotifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_AFlaggedVolumeGetsADateOnAMutedSeries_IsPushedAndTheFlagCleared()
    {
        var series = await AddSeriesAsync("Overlord", muted: true);
        await AddVolumeAsync(series, 17, notifyWhenDated: true);
        await AddEventsAsync(Event(series, 17, Now.AddHours(-1), ShelfEventKind.ReleaseDateChanged));

        await DispatchAsync();

        await _notifier.Received(1).NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
        (await PendingAsync()).ShouldBeEmpty();
        (await IsFlaggedAsync(series, 17)).ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAsync_AnUnflaggedVolumeOnAMutedSeries_IsStillSkipped()
    {
        var series = await AddSeriesAsync("Overlord", muted: true);
        await AddVolumeAsync(series, 17, notifyWhenDated: false);
        await AddEventsAsync(Event(series, 17, Now.AddHours(-1), ShelfEventKind.ReleaseDateChanged));

        await DispatchAsync();

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
        (await PendingAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_AFlaggedVolumeIsOnlyAnnounced_IsSkippedAndKeepsItsFlag()
    {
        var series = await AddSeriesAsync("Overlord", muted: true);
        await AddVolumeAsync(series, 17, notifyWhenDated: true);
        await AddEventsAsync(Event(series, 17, Now.AddHours(-1), ShelfEventKind.VolumeAnnounced));

        await DispatchAsync();

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
        (await IsFlaggedAsync(series, 17)).ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAsync_AFlaggedVolumeWhoseDeliveryShouldRetry_KeepsItsFlagAndItsEvent()
    {
        var series = await AddSeriesAsync("Overlord", muted: true);
        await AddVolumeAsync(series, 17, notifyWhenDated: true);
        await AddEventsAsync(Event(series, 17, Now.AddHours(-1), ShelfEventKind.ReleaseDateChanged));
        Delivers(new NotificationDelivery(Devices: 2, Delivered: 0, FailedTransiently: 2, Removed: 0));

        await DispatchAsync();

        (await PendingAsync()).ShouldHaveSingleItem();
        (await IsFlaggedAsync(series, 17)).ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAsync_AFlaggedVolumesStaleEvent_IsNotSent()
    {
        var series = await AddSeriesAsync("Overlord", muted: true);
        await AddVolumeAsync(series, 17, notifyWhenDated: true);
        await AddEventsAsync(Event(
            series, 17, Now - ReleaseNotificationDispatcher.StaleAfter - TimeSpan.FromHours(1), ShelfEventKind.ReleaseDateChanged));

        await DispatchAsync();

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>());
    }

    private async Task AddVolumeAsync(Series series, decimal number, bool notifyWhenDated)
    {
        var volume = new Volume { Id = Guid.CreateVersion7(), SeriesId = series.Id, Number = number };
        volume.UserState = new VolumeUserState { VolumeId = volume.Id, NotifyWhenDated = notifyWhenDated };

        await using var dbContext = _harness.CreateDbContext();
        dbContext.Volumes.Add(volume);
        await dbContext.SaveChangesAsync();
    }

    private async Task<bool> IsFlaggedAsync(Series series, decimal number)
    {
        await using var dbContext = _harness.CreateDbContext();
        var volume = await dbContext.Volumes.Include(candidate => candidate.UserState)
            .SingleAsync(candidate => candidate.SeriesId == series.Id && candidate.Number == number);

        return volume.UserState!.NotifyWhenDated;
    }

    private void Delivers(NotificationDelivery delivery) =>
        _notifier.NotifyAsync(Arg.Any<ReleaseNotification>(), Arg.Any<CancellationToken>()).Returns(delivery);

    private async Task DispatchAsync()
    {
        await using var dbContext = _harness.CreateDbContext();
        var dispatcher = new ReleaseNotificationDispatcher(
            dbContext, _notifier, _harness.Time, NullLogger<ReleaseNotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(CancellationToken.None);
    }

    private async Task<List<ShelfEvent>> PendingAsync()
    {
        await using var dbContext = _harness.CreateDbContext();
        return await dbContext.ShelfEvents.Where(shelfEvent => shelfEvent.NotifiedAt == null).ToListAsync();
    }

    private async Task<Series> AddSeriesAsync(string title, bool muted = false)
    {
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = title };
        if (muted)
        {
            series.UserState = new SeriesUserState { SeriesId = series.Id, NotificationsMuted = true };
        }

        await using var dbContext = _harness.CreateDbContext();
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series;
    }

    private async Task AddEventsAsync(params ShelfEvent[] events)
    {
        await using var dbContext = _harness.CreateDbContext();
        dbContext.ShelfEvents.AddRange(events);
        await dbContext.SaveChangesAsync();
    }

    private static ShelfEvent Event(
        Series series,
        decimal number,
        DateTimeOffset occurredAt,
        ShelfEventKind kind = ShelfEventKind.VolumeReleased) => new()
    {
        Id = Guid.CreateVersion7(),
        SeriesId = series.Id,
        Kind = kind,
        VolumeNumber = number,
        OccurredAt = occurredAt,
    };
}
