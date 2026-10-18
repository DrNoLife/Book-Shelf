using LnShelf.Api.Features.Preferences.Services;
using LnShelf.Shared.Preferences;

namespace LnShelf.Api.Tests.Features.Preferences.Fixtures;

/// <summary>
/// The real preferences service over a test's own context, for the services that read the
/// reader's defaults. Real rather than substituted so the owner filter and the stored row are
/// part of what the test exercises.
/// </summary>
internal static class TestPreferences
{
    public static PreferencesService For(LnShelfDbContext dbContext) =>
        new(dbContext, NullLogger<PreferencesService>.Instance);

    /// <summary>Saves the reader's choice of progress unit for new series.</summary>
    public static async Task ChooseDefaultProgressUnitAsync(LnShelfDbContext dbContext, ProgressUnit progressUnit) =>
        await For(dbContext).UpdateAsync(
            new UpdatePreferencesRequest { DefaultProgressUnit = progressUnit }, CancellationToken.None);
}
