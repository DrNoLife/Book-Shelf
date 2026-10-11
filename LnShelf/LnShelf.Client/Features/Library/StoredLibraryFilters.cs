namespace LnShelf.Client.Features.Library;

/// <summary>
/// The shelf's filters as this browser keeps them in local storage:
/// <c>{"statuses":["Behind","Reading"],"format":"Manga"}</c>.
/// </summary>
// A serialization shape, so a class of plain strings: the stored value outlives the enums,
// and a name a later release dropped must read as "not remembered", not as an exception.
// LibraryFilterChoice is the clean side; Map below is the only way across.
public sealed class StoredLibraryFilters
{
    public string[]? Statuses { get; set; }

    public string? Format { get; set; }

    public static StoredLibraryFilters From(LibraryFilterChoice choice) => new()
    {
        Statuses = [.. choice.Statuses.Select(status => status.ToString())],
        Format = choice.Format?.ToString(),
    };

    /// <summary>
    /// The choice this stored value describes, or null when it describes none: nothing
    /// stored, or not one status this release knows. An unknown format reads as every format.
    /// </summary>
    public LibraryFilterChoice? ToChoice()
    {
        var statuses = (Statuses ?? []).Select(ParseName<LibraryFilter>).OfType<LibraryFilter>().ToList();
        if (statuses.Count == 0)
        {
            return null;
        }

        return new LibraryFilterChoice(LibraryFilterSet.Normalize(statuses), ParseName<Format>(Format));
    }

    // Names only: Enum.TryParse would also accept "3" and make a member of it.
    private static TEnum? ParseName<TEnum>(string? name)
        where TEnum : struct, Enum =>
        name is not null
        && !int.TryParse(name, out _)
        && Enum.TryParse<TEnum>(name, out var value)
        && Enum.IsDefined(value)
            ? value
            : null;
}
