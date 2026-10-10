namespace LnShelf.Client.Components;

/// <summary>
/// Builds a component's class attribute from its own names plus the class its caller passed
/// through the unmatched attributes.
/// </summary>
// An attribute splat after an explicit class= replaces it, and one before it is replaced by
// it; either way one of the two classes is lost. Merging them is the only way a screen can
// place a shared primitive (a max width, a grid area) without stripping its look.
public static class ClassNames
{
    public static string Join(IReadOnlyDictionary<string, object>? attributes, params string?[] names) =>
        string.Join(
            ' ',
            names
                .Append(attributes?.GetValueOrDefault("class")?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name)));
}
