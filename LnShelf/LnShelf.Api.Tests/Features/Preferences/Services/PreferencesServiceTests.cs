using LnShelf.Api.Data;
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
        response.DefaultLibraryFilter.ShouldBe(LibraryFilter.Behind);

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
        row.Id.ShouldBe(UserPreferences.SingletonId);
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
                DefaultLibraryFilter = LibraryFilter.All,
            },
            CancellationToken.None);

        var response = await service.UpdateAsync(
            new UpdatePreferencesRequest { LibrarySort = LibrarySort.LastRead }, CancellationToken.None);

        response.LibrarySort.ShouldBe(LibrarySort.LastRead);

        response.GridDensity.ShouldBe(GridDensity.Compact);
        response.LibraryView.ShouldBe(LibraryView.Rows);
        response.VolumeView.ShouldBe(VolumeView.List);
        response.ShowVolumeTitles.ShouldBeFalse();
        response.DefaultLibraryFilter.ShouldBe(LibraryFilter.All);
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
            new UpdatePreferencesRequest { DefaultLibraryFilter = LibraryFilter.Backlog },
            CancellationToken.None);

        (await service.GetAsync(CancellationToken.None))
            .DefaultLibraryFilter.ShouldBe(LibraryFilter.Backlog);
    }

    private static PreferencesService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, NullLogger<PreferencesService>.Instance);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"preferences-{Guid.CreateVersion7()}")
            .Options);
}
