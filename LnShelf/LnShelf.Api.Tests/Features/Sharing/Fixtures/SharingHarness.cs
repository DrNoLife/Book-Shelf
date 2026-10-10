using System.Security.Claims;
using LnShelf.Api.Features.Sharing;
using LnShelf.Api.Features.Sharing.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Sharing.Fixtures;

/// <summary>
/// A small real container for the one feature that crosses the owner filters on purpose.
/// </summary>
// Deliberately not TestUsers' fixed current user: the shared read opens a scope of its own and
// calls ActAs there, and only the real scoped CurrentUser over a real IHttpContextAccessor
// (no HttpContext = an anonymous request) makes that the production code path rather than a
// fake that would agree with anything. The context is the real LnShelfDbContext with its real
// owner filters, over an in-memory store; the Sharing services are registered by AddSharing
// itself, so a registration mistake shows up here too.
internal sealed class SharingHarness : IDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 10);

    public const string OwnerReadingTitle = "The Saga of Tanya the Evil";
    public const string OwnerDroppedTitle = "Overlord";
    public const string SomeoneElsesTitle = "Mushoku Tensei";

    public const string OwnerReadingCover = "anilist/1001.jpg";
    public const string OwnerDroppedCover = "anilist/1002.jpg";
    public const string SomeoneElsesCover = "anilist/2001.jpg";

    private readonly ServiceProvider _provider;

    public SharingHarness()
    {
        var database = $"sharing-{Guid.CreateVersion7()}";
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(HttpContextAccessor);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(Today, new TimeOnly(12, 0), TimeSpan.Zero)));
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddDbContext<LnShelfDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddSharing(new ConfigurationBuilder().Build());

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Who the request is from. No HttpContext (the default) is an anonymous visitor.</summary>
    public IHttpContextAccessor HttpContextAccessor { get; } = new HttpContextAccessor();

    /// <summary>
    /// The default two shelves. The owner: a series being read (one volume read, one out and
    /// unread) and a dropped one. Someone else: one series of their own, one volume read.
    /// </summary>
    public async Task SeedTwoShelvesAsync()
    {
        await AddSeriesAsync(
            TestUsers.Owner,
            NewSeries(OwnerReadingTitle, TrackingStatus.Reading, OwnerReadingCover,
                NewVolume(1, ReadStatus.Read, owned: true, isReleased: true),
                NewVolume(2, ReadStatus.Unread, owned: false, isReleased: true)),
            NewSeries(OwnerDroppedTitle, TrackingStatus.Dropped, OwnerDroppedCover,
                NewVolume(1, ReadStatus.Read, owned: true, isReleased: true)));

        await AddSeriesAsync(
            TestUsers.SomeoneElse,
            NewSeries(SomeoneElsesTitle, TrackingStatus.Reading, SomeoneElsesCover,
                NewVolume(1, ReadStatus.Read, owned: true, isReleased: true)));
    }

    /// <summary>Adds series to a user's shelf, written as that user so the context stamps the owner.</summary>
    public async Task AddSeriesAsync(Guid ownerId, params Series[] series)
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(ownerId);
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        if (await dbContext.Users.FindAsync(ownerId) is null)
        {
            dbContext.Users.Add(new User
            {
                Id = ownerId,
                Issuer = "https://issuer.test",
                Subject = ownerId.ToString(),
                Email = $"{ownerId:N}@example.com",
            });
        }

        dbContext.Series.AddRange(series);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Makes a user's shelf public through the real command service, and returns the token.</summary>
    public async Task<string> ShareAsync(Guid userId)
    {
        var sharing = await AsUserAsync(
            userId,
            services => services.GetRequiredService<IShelfShareCommandService>().SetVisibilityAsync(true, CancellationToken.None));

        return sharing.Token.ShouldNotBeNull();
    }

    /// <summary>Work in a scope acting for <paramref name="userId"/>, as background work would.</summary>
    public async Task<T> AsUserAsync<T>(Guid userId, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);

        return await work(scope.ServiceProvider);
    }

    /// <summary>Work in a request scope: whoever <see cref="HttpContextAccessor"/> says, nobody by default.</summary>
    public async Task<T> AsRequestAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    /// <summary>A request scope the test keeps, to look at it after the work.</summary>
    public AsyncServiceScope CreateRequestScope() => _provider.CreateAsyncScope();

    /// <summary>The request carries this user's session, as UserSignIn writes it.</summary>
    public void SignInVisitor(Guid userId) =>
        HttpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(LnShelfClaims.UserId, userId.ToString())], "test")),
        };

    public static Series NewSeries(string title, TrackingStatus? status, string? cover, params Volume[] volumes)
    {
        var seriesId = Guid.CreateVersion7();
        var series = new Series
        {
            Id = seriesId,
            TitleEnglish = title,
            TitleRomaji = $"{title} (romaji)",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            CoverObjectKey = cover,
            UserState = status is { } tracking ? new SeriesUserState { SeriesId = seriesId, TrackingStatus = tracking } : null,
        };

        foreach (var volume in volumes)
        {
            volume.SeriesId = seriesId;
            series.Volumes.Add(volume);
        }

        return series;
    }

    public static Volume NewVolume(
        int number,
        ReadStatus readStatus,
        bool owned,
        bool isReleased,
        DateOnly? releaseDate = null,
        string? coverOverride = null)
    {
        var volumeId = Guid.CreateVersion7();

        return new Volume
        {
            Id = volumeId,
            Number = number,
            IsReleased = isReleased,
            ReleaseDate = releaseDate,
            UserState = new VolumeUserState
            {
                VolumeId = volumeId,
                ReadStatus = readStatus,
                Owned = owned,
                PurchasePrice = owned ? 99m : null,
                CoverOverrideObjectKey = coverOverride,
            },
        };
    }

    public void Dispose()
    {
        HttpContextAccessor.HttpContext = null;
        _provider.Dispose();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
