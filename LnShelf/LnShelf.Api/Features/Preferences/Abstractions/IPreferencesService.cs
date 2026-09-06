namespace LnShelf.Api.Features.Preferences.Abstractions;

/// <summary>
/// Reads and writes the single preferences row.
/// </summary>
// Not split into query and command services: it is one row with one getter and one
// partial setter, and two classes for that would be ceremony rather than the
// separation CLAUDE.md is asking for.
public interface IPreferencesService
{
    Task<PreferencesResponse> GetAsync(CancellationToken cancellationToken);

    /// <summary>Partial update — every null leaves that setting alone.</summary>
    Task<PreferencesResponse> UpdateAsync(
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken);
}
