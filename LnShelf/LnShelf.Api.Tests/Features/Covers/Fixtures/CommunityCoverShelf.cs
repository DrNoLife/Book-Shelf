using LnShelf.Api.Features.Covers.Services;
using LnShelf.Api.Features.Preferences.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LnShelf.Api.Tests.Features.Covers.Fixtures;

/// <summary>
/// Several users on one in-memory database, each acting through a context of their own, with
/// the real cover-sharing services over a shared in-memory store.
/// </summary>
internal static class CommunityCoverShelf
{
    public static readonly Guid Third = new("33333333-3333-3333-3333-333333333333");

    public static string NewDatabase() => $"community-covers-{Guid.CreateVersion7()}";

    public static LnShelfDbContext CreateDbContext(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));

    /// <summary>A real PNG, because the normalizer decodes what it is given.</summary>
    public static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }

    public static async Task AddUserAsync(string database, Guid userId, string email)
    {
        await using var dbContext = CreateDbContext(database, userId);
        dbContext.Users.Add(new User { Id = userId, Issuer = "test", Subject = $"sub-{userId}", Email = email });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Gives a user a one-volume series and returns the volume's id.</summary>
    public static async Task<Guid> SeedVolumeAsync(
        string database,
        Guid userId,
        decimal number = 1,
        int? aniListId = 94846,
        long? mangaUpdatesId = null,
        Format format = Format.LightNovel,
        string? overrideObjectKey = null)
    {
        await using var dbContext = CreateDbContext(database, userId);

        var volumeId = Guid.CreateVersion7();
        dbContext.Series.Add(new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            Format = format,
            TitleEnglish = "The Saga of Tanya the Evil",
            PublicationStatus = PublicationStatus.Ongoing,
            Volumes =
            [
                new Volume
                {
                    Id = volumeId,
                    Number = number,
                    IsReleased = true,
                    UserState = overrideObjectKey is null
                        ? null
                        : new VolumeUserState { VolumeId = volumeId, CoverOverrideObjectKey = overrideObjectKey },
                },
            ],
        });
        await dbContext.SaveChangesAsync();

        return volumeId;
    }

    public static async Task EnableSharingAsync(string database, Guid userId)
    {
        await using var dbContext = CreateDbContext(database, userId);
        dbContext.UserPreferences.Add(new UserPreferences
        {
            CoverSharingSince = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Puts a published row and its object in place directly, without going through an upload,
    /// so a test can fix the hash and the time.
    /// </summary>
    public static async Task<CommunityCover> PublishAsync(
        string database,
        Guid uploaderId,
        InMemoryCoverStore store,
        byte[] content,
        DateTimeOffset publishedAt,
        int? aniListId = 94846,
        long? mangaUpdatesId = null,
        Format format = Format.LightNovel,
        decimal number = 1)
    {
        await using var dbContext = CreateDbContext(database, uploaderId);

        var sourceVolumeId = Guid.CreateVersion7();
        var objectKey = CoverObjectKey.ForVolumeOverride(sourceVolumeId, Guid.CreateVersion7(), ".webp");
        store.Put(objectKey, content);

        var cover = new CommunityCover
        {
            Id = Guid.CreateVersion7(),
            UploaderUserId = uploaderId,
            SourceVolumeId = sourceVolumeId,
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            Format = format,
            Number = number,
            ContentHash = CoverContentHash.Compute(content),
            ObjectKey = objectKey,
            PublishedAt = publishedAt,
        };
        dbContext.CommunityCovers.Add(cover);
        await dbContext.SaveChangesAsync();

        return cover;
    }

    public static CommunityCoverQueryService Queries(
        LnShelfDbContext dbContext, Guid userId, InMemoryCoverStore store) =>
        new(dbContext, store, TestUsers.As(userId));

    public static CommunityCoverCommandService Commands(
        LnShelfDbContext dbContext, Guid userId, ICoverStore store) =>
        new(dbContext, store, TestUsers.As(userId), TimeProvider.System, NullLogger<CommunityCoverCommandService>.Instance);

    public static VolumeCoverCommandService CoverCommands(
        LnShelfDbContext dbContext,
        Guid userId,
        ICoverStore store,
        UserLimits? userLimits = null) =>
        new(dbContext,
            store,
            new CoverNormalizer(Options.Create(new CoverImageOptions()), NullLogger<CoverNormalizer>.Instance),
            Commands(dbContext, userId, store),
            new CommunityCoverQueryService(dbContext, store, TestUsers.As(userId)),
            TimeProvider.System,
            userLimits ?? TestUsers.Limits(dbContext),
            NullLogger<VolumeCoverCommandService>.Instance);
}
