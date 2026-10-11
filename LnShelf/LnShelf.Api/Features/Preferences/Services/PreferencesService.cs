using LnShelf.Api.Features.Preferences.Abstractions;
using LnShelf.Api.Features.Preferences.Mappers;

namespace LnShelf.Api.Features.Preferences.Services;

/// <inheritdoc />
public sealed class PreferencesService(LnShelfDbContext dbContext, ILogger<PreferencesService> logger)
    : IPreferencesService
{
    public async Task<PreferencesResponse> GetAsync(CancellationToken cancellationToken)
    {
        var preferences = await dbContext.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        // The owner filter leaves this user's row or none. Absent means "never changed
        // anything", which is the defaults — not an error
        // and not a reason to write a row before the user has an opinion.
        return PreferencesMapper.ToResponse(preferences ?? new UserPreferences());
    }

    public async Task<PreferencesResponse> UpdateAsync(
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preferences = await dbContext.UserPreferences
            .FirstOrDefaultAsync(cancellationToken);

        if (preferences is null)
        {
            preferences = new UserPreferences();
            dbContext.UserPreferences.Add(preferences);
        }

        // Every null leaves that setting alone, so the density toggle does not have to
        // send the sort order back just to avoid clobbering it.
        preferences.GridDensity = request.GridDensity ?? preferences.GridDensity;
        preferences.LibrarySort = request.LibrarySort ?? preferences.LibrarySort;
        preferences.LibraryView = request.LibraryView ?? preferences.LibraryView;
        preferences.VolumeView = request.VolumeView ?? preferences.VolumeView;
        preferences.ShowVolumeTitles = request.ShowVolumeTitles ?? preferences.ShowVolumeTitles;

        if (request.DefaultLibraryFilters is { } filters)
        {
            preferences.DefaultLibraryFilters = LibraryFilterSet.Normalize(filters);
        }

        if (request.LibrarySectionOrder is { } order)
        {
            preferences.LibrarySectionOrder = LibrarySections.Normalize(order);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Preferences saved: density {GridDensity}, sort {LibrarySort}, library view {LibraryView}, "
            + "volume view {VolumeView}, volume titles {ShowVolumeTitles}, default filters {DefaultLibraryFilters}, "
            + "section order {LibrarySectionOrder}",
            preferences.GridDensity, preferences.LibrarySort, preferences.LibraryView,
            preferences.VolumeView, preferences.ShowVolumeTitles,
            preferences.DefaultLibraryFilters, preferences.LibrarySectionOrder);

        return PreferencesMapper.ToResponse(preferences);
    }
}
