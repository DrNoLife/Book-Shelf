using System.Text.Json;
using LnShelf.Api.Features.Calendar.Models;
using LnShelf.Api.Features.Notifications.Models;
using LnShelf.Api.Features.Preferences.Models;
using LnShelf.Api.Features.Sharing.Models;
using LnShelf.Shared.Auth;
using LnShelf.Shared.Preferences;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>
/// "Export my data" (GDPR Art. 15 and 20) holds everything the signed-in user owns, and
/// nothing that belongs to anyone else.
/// </summary>
// The leak is the silent failure here, so the second user's data is distinctive on every
// table the export reads, and the check is on the serialised file: whatever the export says,
// it must not say any of it.
public sealed class AccountExportQueryServiceTests
{
    [Fact]
    public async Task ExportAsync_TwoUsersWithData_ContainsEveryTableOfTheCallerOnly()
    {
        var database = $"export-{Guid.CreateVersion7()}";
        await SeedAsync(database, TestUsers.Owner, "owner");
        await SeedAsync(database, TestUsers.SomeoneElse, "other");

        var export = await ExportAsync(database, TestUsers.Owner);

        export.Account.Email.ShouldBe("owner@example.com");
        export.Account.Subject.ShouldBe("sub-owner");
        export.Sharing.ShouldBe(new AccountExportSharing
        {
            IsPublic = true,
            Token = "token-owner",
            TokenCreatedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
        });
        export.Calendar.ShouldBe(new AccountExportCalendar
        {
            IsOn = true,
            Token = "calendar-owner",
            TokenCreatedAt = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
        });
        var device = export.NotificationDevices.ShouldHaveSingleItem();
        device.PushServiceHost.ShouldBe("fcm.googleapis.com");
        device.DeviceLabel.ShouldBe("Device of owner");
        export.Preferences.GridDensity.ShouldBe(GridDensity.Compact);
        export.ActivitySeenAt.ShouldBe(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        var series = export.Series.ShouldHaveSingleItem();
        series.TitleEnglish.ShouldBe("Title of owner");
        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.EnglishPublisherNote);
        series.VolumeCountEvidence.ShouldBe("Evidence of owner");
        var shelfEvent = series.Events.ShouldHaveSingleItem();
        shelfEvent.Kind.ShouldBe(ShelfEventKind.ReleaseDateChanged);
        shelfEvent.VolumeNumber.ShouldBe(3);
        shelfEvent.NewDate.ShouldBe(new DateOnly(2026, 12, 12));
        series.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.Notes.ShouldBe("Notes of owner");
        series.Volumes.Select(volume => volume.Number).ShouldBe([1, 2]);

        var first = series.Volumes[0];
        first.ReadStatus.ShouldBe(ReadStatus.Read);
        first.Owned.ShouldBeTrue();
        first.PurchasePrice.ShouldBe(79.95m);
        first.CoverOverrideObjectKey.ShouldBe("override/volume/owner-cover");

        // A volume nothing was recorded on is Unread and NotOwned, as everywhere else.
        series.Volumes[1].ReadStatus.ShouldBe(ReadStatus.Unread);
        series.Volumes[1].Owned.ShouldBeFalse();

        var batch = export.ImportBatches.ShouldHaveSingleItem();
        batch.FileName.ShouldBe("file-of-owner.csv");
        var row = batch.Groups.ShouldHaveSingleItem().Rows.ShouldHaveSingleItem();
        row.RawTitle.ShouldBe("Raw title of owner, Vol. 1");

        var run = export.SyncRuns.ShouldHaveSingleItem();
        run.Summary.ShouldBe("Summary of owner");
        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.SeriesTitle.ShouldBe("Title of owner");
        discrepancy.Values.Select(value => value.Value).ShouldBe(["16", "17"]);
    }

    [Fact]
    public async Task ExportAsync_TwoUsersWithData_NeverSerialisesAnythingOfTheOtherUser()
    {
        var database = $"export-{Guid.CreateVersion7()}";
        await SeedAsync(database, TestUsers.Owner, "owner");
        await SeedAsync(database, TestUsers.SomeoneElse, "other");

        var file = JsonSerializer.Serialize(await ExportAsync(database, TestUsers.Owner));

        file.ShouldContain("owner");
        file.ShouldNotContain("other", Case.Insensitive, "the export carries something of another user");
        file.ShouldNotContain(TestUsers.SomeoneElse.ToString());
    }

    // The endpoint and the two keys are what it takes to push to a device; the export names
    // the push service and the device, and carries none of the three.
    [Fact]
    public async Task ExportAsync_ADeviceWithNotificationsOn_LeavesOutItsEndpointAndKeys()
    {
        var database = $"export-{Guid.CreateVersion7()}";
        await SeedAsync(database, TestUsers.Owner, "owner");

        var file = JsonSerializer.Serialize(await ExportAsync(database, TestUsers.Owner));

        file.ShouldContain("Device of owner");
        file.ShouldNotContain("endpoint-owner");
        file.ShouldNotContain("p256dh-owner");
        file.ShouldNotContain("auth-owner");
    }

    [Fact]
    public async Task ExportAsync_ASignedInUserWithNothingRecorded_IsAnAccountAndDefaults()
    {
        var database = $"export-{Guid.CreateVersion7()}";
        await using (var dbContext = Context(database, TestUsers.Owner))
        {
            dbContext.Users.Add(NewUser(TestUsers.Owner, "owner"));
            await dbContext.SaveChangesAsync();
        }

        var export = await ExportAsync(database, TestUsers.Owner);

        export.Sharing.ShouldBe(new AccountExportSharing { IsPublic = false });
        export.Calendar.ShouldBe(new AccountExportCalendar { IsOn = false });
        export.NotificationDevices.ShouldBeEmpty();
        export.Preferences.GridDensity.ShouldBe(GridDensity.Comfortable);
        export.ActivitySeenAt.ShouldBeNull();
        export.Series.ShouldBeEmpty();
        export.ImportBatches.ShouldBeEmpty();
        export.SyncRuns.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExportAsync_TheUsersRowIsGone_ReturnsNull()
    {
        var export = await ExportServiceAsync($"export-{Guid.CreateVersion7()}", TestUsers.Owner);

        export.ShouldBeNull();
    }

    private static async Task<AccountExportResponse> ExportAsync(string database, Guid userId) =>
        (await ExportServiceAsync(database, userId)).ShouldNotBeNull();

    private static async Task<AccountExportResponse?> ExportServiceAsync(string database, Guid userId)
    {
        await using var dbContext = Context(database, userId);
        var service = new AccountExportQueryService(dbContext, TestUsers.As(userId), TimeProvider.System);

        return await service.ExportAsync(CancellationToken.None);
    }

    /// <summary>One user's account, link, preferences, shelf, import and sync history, every string marked with <paramref name="marker"/>.</summary>
    private static async Task SeedAsync(string database, Guid userId, string marker)
    {
        await using var dbContext = Context(database, userId);

        dbContext.Users.Add(NewUser(userId, marker));
        dbContext.ShelfShares.Add(new ShelfShare
        {
            UserId = userId,
            Token = $"token-{marker}",
            CreatedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
        });
        dbContext.CalendarFeeds.Add(new CalendarFeed
        {
            UserId = userId,
            Token = $"calendar-{marker}",
            CreatedAt = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
        });
        dbContext.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.CreateVersion7(),
            Endpoint = $"https://fcm.googleapis.com/fcm/send/endpoint-{marker}",
            P256dh = $"p256dh-{marker}",
            Auth = $"auth-{marker}",
            DeviceLabel = $"Device of {marker}",
            CreatedAt = new DateTimeOffset(2026, 10, 4, 13, 0, 0, TimeSpan.Zero),
        });
        dbContext.UserPreferences.Add(new UserPreferences
        {
            GridDensity = GridDensity.Compact,
            ActivitySeenAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
        });

        var seriesId = Guid.CreateVersion7();
        var firstVolumeId = Guid.CreateVersion7();
        dbContext.ShelfEvents.Add(new ShelfEvent
        {
            Id = Guid.CreateVersion7(),
            SeriesId = seriesId,
            Kind = ShelfEventKind.ReleaseDateChanged,
            VolumeNumber = 3,
            NewDate = new DateOnly(2026, 12, 12),
            OccurredAt = new DateTimeOffset(2026, 10, 3, 4, 5, 0, TimeSpan.Zero),
        });
        dbContext.Series.Add(new Series
        {
            Id = seriesId,
            TitleEnglish = $"Title of {marker}",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            VolumeCountBasis = VolumeCountBasis.EnglishPublisherNote,
            VolumeCountEvidence = $"Evidence of {marker}",
            UserState = new SeriesUserState
            {
                SeriesId = seriesId,
                TrackingStatus = TrackingStatus.Reading,
                Notes = $"Notes of {marker}",
            },
            Volumes =
            [
                new Volume
                {
                    Id = firstVolumeId,
                    Number = 1,
                    UserState = new VolumeUserState
                    {
                        VolumeId = firstVolumeId,
                        ReadStatus = ReadStatus.Read,
                        Owned = true,
                        PurchasePrice = 79.95m,
                        CoverOverrideObjectKey = $"override/volume/{marker}-cover",
                    },
                },
                new Volume { Id = Guid.CreateVersion7(), Number = 2 },
            ],
        });

        var batchId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        dbContext.ImportBatches.Add(new ImportBatch
        {
            Id = batchId,
            FileName = $"file-of-{marker}.csv",
            UploadedAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero),
            RowCount = 1,
            Groups =
            [
                new ImportGroup
                {
                    Id = groupId,
                    ImportBatchId = batchId,
                    SeriesKey = $"key-{marker}",
                    SeriesTitle = $"Title of {marker}",
                    Rows =
                    [
                        new ImportRow
                        {
                            Id = Guid.CreateVersion7(),
                            ImportGroupId = groupId,
                            LineNumber = 2,
                            RawTitle = $"Raw title of {marker}, Vol. 1",
                        },
                    ],
                },
            ],
        });

        var runId = Guid.CreateVersion7();
        var discrepancyId = Guid.CreateVersion7();
        dbContext.SyncRuns.Add(new SyncRun
        {
            Id = runId,
            StartedAt = new DateTimeOffset(2026, 10, 3, 4, 0, 0, TimeSpan.Zero),
            Summary = $"Summary of {marker}",
            Discrepancies =
            [
                new SyncDiscrepancy
                {
                    Id = discrepancyId,
                    SyncRunId = runId,
                    SeriesId = seriesId,
                    SeriesTitle = $"Title of {marker}",
                    Field = SyncDiscrepancyField.VolumeCount,
                }
                    .Says(MetadataSourceName.AniList, "16")
                    .Says(MetadataSourceName.MangaUpdates, "17"),
            ],
        });

        await dbContext.SaveChangesAsync();
    }

    private static User NewUser(Guid id, string marker) => new()
    {
        Id = id,
        Issuer = "https://accounts.google.com",
        Subject = $"sub-{marker}",
        Email = $"{marker}@example.com",
        CreatedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        LastSeenAt = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero),
    };

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));
}
