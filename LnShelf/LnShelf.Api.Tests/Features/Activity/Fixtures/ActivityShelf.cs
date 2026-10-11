using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Activity.Fixtures;

/// <summary>
/// A shelf with events on it, seeded directly: emission is the sync's business, and these
/// tests are about reading what it left.
/// </summary>
public static class ActivityShelf
{
    public static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    public static Guid AddSeries(LnShelfDbContext dbContext, string title)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var seriesId = Guid.CreateVersion7();
        dbContext.Series.Add(new Series { Id = seriesId, TitleEnglish = title, Format = Format.LightNovel });
        return seriesId;
    }

    public static void AddEvent(LnShelfDbContext dbContext, Guid seriesId, decimal volumeNumber, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        dbContext.ShelfEvents.Add(new ShelfEvent
        {
            Id = Guid.CreateVersion7(),
            SeriesId = seriesId,
            Kind = ShelfEventKind.VolumeReleased,
            VolumeNumber = volumeNumber,
            OccurredAt = occurredAt,
        });
    }

    /// <summary>A clock stopped at <see cref="Now"/>.</summary>
    public static TimeProvider FixedClock()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);
        return timeProvider;
    }

    public static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"activity-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
