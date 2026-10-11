using LnShelf.Api.Features.Preferences.Data;
using LnShelf.Api.Features.Preferences.Models;
using LnShelf.Api.Features.Preferences.Services;
using LnShelf.Shared.Preferences;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Preferences.Services;

/// <summary>
/// The partial-update rule: every null leaves that setting alone, so the density toggle
/// does not have to send the sort order back just to avoid clobbering it.
/// </summary>
// Silent-data territory under testing.md: getting this wrong loses a setting the user
// chose, and the only symptom is a preference quietly reverting later.
public sealed class PreferencesServiceTests
{
    [Fact]
    public async Task GetAsync_NoRowYet_IsTheDefaultsAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreateService(dbContext).GetAsync(CancellationToken.None);

        response.GridDensity.ShouldBe(GridDensity.Comfortable);
        response.LibrarySort.ShouldBe(LibrarySort.MostBehind);
        response.LibraryView.ShouldBe(LibraryView.Covers);
        response.VolumeView.ShouldBe(VolumeView.Tiles);
        response.ShowVolumeTitles.ShouldBeTrue();
        response.DefaultLibraryFilters.ShouldBe([LibraryFilter.Behind]);
        response.LibrarySectionOrder.ShouldBe(
            [LibrarySection.ContinueReading, LibrarySection.OutNow, LibrarySection.Shelf]);

        // Absent means "never changed anything", which is not a reason to write a row
        // before the user has an opinion.
        (await dbContext.UserPreferences.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateAsync_TheFirstChange_CreatesTheSingletonRow()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreateService(dbContext).UpdateAsync(
            new UpdatePreferencesRequest { GridDensity = GridDensity.Compact }, CancellationToken.None);

        response.GridDensity.ShouldBe(GridDensity.Compact);

        var row = await dbContext.UserPreferences.SingleAsync();
        row.OwnerId.ShouldBe(TestUsers.Owner);
        row.GridDensity.ShouldBe(GridDensity.Compact);
    }

    /// <summary>
    /// The whole rule, one setting at a time: sending only this field must move only this
    /// field and leave the other five exactly as they were.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_OneSettingAtATime_LeavesEveryOtherSettingAlone()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        // Start from a row where every setting is the opposite of its default, so a
        // clobber back to the default is visible rather than accidentally right.
        await service.UpdateAsync(
            new UpdatePreferencesRequest
            {
                GridDensity = GridDensity.Compact,
                LibrarySort = LibrarySort.Alphabetical,
                LibraryView = LibraryView.Rows,
                VolumeView = VolumeView.List,
                ShowVolumeTitles = false,
                DefaultLibraryFilters = [LibraryFilter.Owned, LibraryFilter.Reading],
                LibrarySectionOrder = [LibrarySection.Shelf, LibrarySection.OutNow, LibrarySection.ContinueReading],
            },
            CancellationToken.None);

        var response = await service.UpdateAsync(
            new UpdatePreferencesRequest { LibrarySort = LibrarySort.LastRead }, CancellationToken.None);

        response.LibrarySort.ShouldBe(LibrarySort.LastRead);

        response.GridDensity.ShouldBe(GridDensity.Compact);
        response.LibraryView.ShouldBe(LibraryView.Rows);
        response.VolumeView.ShouldBe(VolumeView.List);
        response.ShowVolumeTitles.ShouldBeFalse();
        response.DefaultLibraryFilters.ShouldBe([LibraryFilter.Owned, LibraryFilter.Reading]);
        response.LibrarySectionOrder.ShouldBe(
            [LibrarySection.Shelf, LibrarySection.OutNow, LibrarySection.ContinueReading]);
    }

    /// <summary>
    /// The one setting a nullable-means-unchanged rule can get subtly wrong: `false` is a
    /// value, not an omission.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShowVolumeTitlesSetToFalse_IsStoredRatherThanTreatedAsOmitted()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var response = await service.UpdateAsync(
            new UpdatePreferencesRequest { ShowVolumeTitles = false }, CancellationToken.None);

        response.ShowVolumeTitles.ShouldBeFalse();
        (await dbContext.UserPreferences.SingleAsync()).ShowVolumeTitles.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_AnEmptyRequest_ChangesNothing()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateAsync(
            new UpdatePreferencesRequest { VolumeView = VolumeView.List }, CancellationToken.None);

        var response = await service.UpdateAsync(new UpdatePreferencesRequest(), CancellationToken.None);

        response.VolumeView.ShouldBe(VolumeView.List);
    }

    [Fact]
    public async Task UpdateAsync_TwiceInARow_KeepsOneRow()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateAsync(
            new UpdatePreferencesRequest { GridDensity = GridDensity.Compact }, CancellationToken.None);
        await service.UpdateAsync(
            new UpdatePreferencesRequest { GridDensity = GridDensity.Comfortable }, CancellationToken.None);

        // "The settings" is a singleton; a table that could hold two of them would invite
        // code that has to choose between them.
        (await dbContext.UserPreferences.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_AfterAnUpdate_ReadsBackWhatWasSaved()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateAsync(
            new UpdatePreferencesRequest
            {
                DefaultLibraryFilters = [LibraryFilter.Backlog, LibraryFilter.Behind],
                LibrarySectionOrder = [LibrarySection.Shelf],
            },
            CancellationToken.None);

        var response = await service.GetAsync(CancellationToken.None);

        response.DefaultLibraryFilters.ShouldBe([LibraryFilter.Behind, LibraryFilter.Backlog]);
        response.LibrarySectionOrder.ShouldBe(
            [LibrarySection.Shelf, LibrarySection.ContinueReading, LibrarySection.OutNow]);
    }

    /// <summary>
    /// What is saved is the normalised set: the same answer the shelf would give for it.
    /// </summary>
    [Theory]
    [MemberData(nameof(DefaultFilterSets))]
    public async Task UpdateAsync_DefaultLibraryFilters_StoresTheNormalisedSet(
        LibraryFilter[] sent,
        LibraryFilter[] stored)
    {
        await using var dbContext = CreateDbContext();

        var response = await CreateService(dbContext).UpdateAsync(
            new UpdatePreferencesRequest { DefaultLibraryFilters = sent }, CancellationToken.None);

        response.DefaultLibraryFilters.ShouldBe(stored);
        (await dbContext.UserPreferences.SingleAsync()).DefaultLibraryFilters.ShouldBe(stored);
    }

    public static TheoryData<LibraryFilter[], LibraryFilter[]> DefaultFilterSets => new()
    {
        { [LibraryFilter.Owned], [LibraryFilter.Owned] },
        { [LibraryFilter.Reading, LibraryFilter.Owned, LibraryFilter.Reading], [LibraryFilter.Owned, LibraryFilter.Reading] },
        { [LibraryFilter.Behind, LibraryFilter.All], [LibraryFilter.All] },
        { [], [LibraryFilter.All] },
    };

    /// <summary>
    /// The column outlives the enum: a name a later release dropped must not make the row
    /// unreadable, and the order that comes back still holds every section.
    /// </summary>
    [Fact]
    public async Task GetAsync_StoredNamesTheEnumDoesNotKnow_AreSkippedAndTheOrderCompleted()
    {
        await using var dbContext = CreateDbContext();
        dbContext.UserPreferences.Add(new UserPreferences
        {
            OwnerId = TestUsers.Owner,
            DefaultLibraryFilters = EnumListConverter<LibraryFilter>.FromText("Reading,Retired, Owned"),
            LibrarySectionOrder = EnumListConverter<LibrarySection>.FromText("OutNow,Gone,OutNow"),
        });
        await dbContext.SaveChangesAsync();

        var response = await CreateService(dbContext).GetAsync(CancellationToken.None);

        response.DefaultLibraryFilters.ShouldBe([LibraryFilter.Owned, LibraryFilter.Reading]);
        response.LibrarySectionOrder.ShouldBe(
            [LibrarySection.OutNow, LibrarySection.ContinueReading, LibrarySection.Shelf]);
    }

    private static PreferencesService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, NullLogger<PreferencesService>.Instance);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"preferences-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
