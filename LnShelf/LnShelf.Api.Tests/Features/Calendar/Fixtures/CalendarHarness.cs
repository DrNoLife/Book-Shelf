using LnShelf.Api.Features.Calendar;
using LnShelf.Api.Features.Calendar.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Calendar.Fixtures;

/// <summary>
/// A small real container for the release calendar, which reads a shelf from an anonymous
/// request the way the shared shelf does.
/// </summary>
// SharingHarness' reasoning: the real scoped CurrentUser over a real IHttpContextAccessor
// (no HttpContext = an anonymous request), the real LnShelfDbContext with its owner filters
// over an in-memory store, and the slice's services registered by AddCalendar itself.
internal sealed class CalendarHarness : IDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 11);

    private readonly ServiceProvider _provider;

    public CalendarHarness()
    {
        var database = $"calendar-{Guid.CreateVersion7()}";
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(Today, new TimeOnly(12, 0), TimeSpan.Zero)));
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddDbContext<LnShelfDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddCalendar(new ConfigurationBuilder().Build());

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Adds series to a user's shelf, written as that user so the context stamps the owner.</summary>
    public async Task AddSeriesAsync(Guid ownerId, params Series[] series)
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(ownerId);
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        await EnsureUserAsync(dbContext, ownerId);

        dbContext.Series.AddRange(series);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Turns a user's calendar on through the real command service, and returns the token.</summary>
    public async Task<string> TurnOnAsync(Guid userId)
    {
        await using (var scope = _provider.CreateAsyncScope())
        {
            await EnsureUserAsync(scope.ServiceProvider.GetRequiredService<LnShelfDbContext>(), userId);
        }

        var feed = await AsUserAsync(
            userId,
            services => services.GetRequiredService<ICalendarFeedCommandService>().SetOnAsync(true, CancellationToken.None));

        return feed.Token.ShouldNotBeNull();
    }

    /// <summary>Work in a scope acting for <paramref name="userId"/>, as a signed-in request would.</summary>
    public async Task<T> AsUserAsync<T>(Guid userId, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);

        return await work(scope.ServiceProvider);
    }

    /// <summary>Work in an anonymous request scope, as a calendar app's fetch is.</summary>
    public async Task<T> AsAnonymousAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    public static Series NewSeries(string title, TrackingStatus? status, params Volume[] volumes)
    {
        var seriesId = Guid.CreateVersion7();
        var series = new Series
        {
            Id = seriesId,
            TitleEnglish = title,
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = status is { } tracking ? new SeriesUserState { SeriesId = seriesId, TrackingStatus = tracking } : null,
        };

        foreach (var volume in volumes)
        {
            volume.SeriesId = seriesId;
            series.Volumes.Add(volume);
        }

        return series;
    }

    public static Volume NewVolume(decimal number, DateOnly? releaseDate, bool owned = false)
    {
        var volumeId = Guid.CreateVersion7();

        return new Volume
        {
            Id = volumeId,
            Number = number,
            ReleaseDate = releaseDate,
            IsReleased = releaseDate is { } date && date <= Today,
            UserState = owned ? new VolumeUserState { VolumeId = volumeId, Owned = true } : null,
        };
    }

    public void Dispose() => _provider.Dispose();

    private static async Task EnsureUserAsync(LnShelfDbContext dbContext, Guid userId)
    {
        if (await dbContext.Users.FindAsync(userId) is not null)
        {
            return;
        }

        dbContext.Users.Add(new User
        {
            Id = userId,
            Issuer = "https://issuer.test",
            Subject = userId.ToString(),
            Email = $"{userId:N}@example.com",
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
