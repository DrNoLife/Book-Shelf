using System.Text.Json;

namespace LnShelf.Client.Features.Library;

/// <summary>
/// Remembers the shelf's status and format filters in this browser's local storage, so the
/// shelf opens the way it was left. The title filter is not remembered: it is a search, not
/// a setting.
/// </summary>
// Per browser rather than a server preference, like the colour key: the phone in the evening
// and the desktop on a weekend are asked different questions of the same shelf. The account's
// own default (DefaultLibraryFilters) is what a browser with nothing remembered starts from.
public sealed class LibraryFilterMemory(IJSRuntime javaScript)
{
    /// <summary>The localStorage name; the privacy page lists it.</summary>
    public const string StorageKey = "lnshelf.library-filters";

    public async Task<LibraryFilterChoice?> ReadAsync()
    {
        try
        {
            var stored = await javaScript.InvokeAsync<StoredLibraryFilters?>("lnshelf.readJson", StorageKey);
            return stored?.ToChoice();
        }
        // Valid JSON of the wrong shape (edited by hand, or an older format) fails to bind, and
        // the interop layer reports that wrapped in a JSException. Either way nothing usable is
        // remembered, which is the honest reading.
        catch (Exception exception) when (exception is JsonException or JSException)
        {
            return null;
        }
    }

    public async Task RememberAsync(LibraryFilterChoice choice) =>
        await javaScript.InvokeVoidAsync("lnshelf.writeJson", StorageKey, StoredLibraryFilters.From(choice));

    /// <summary>Back to the account's default next time: what "Clear all" means.</summary>
    public async Task ForgetAsync() =>
        await javaScript.InvokeVoidAsync("lnshelf.writeJson", StorageKey, null);
}
