using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LnShelf.Api.Features.Preferences.Data;

/// <summary>
/// Stores an ordered list of enum values as their names, comma separated: "Behind,Reading".
/// </summary>
// Reading skips a name the enum no longer has instead of throwing: a member renamed or
// removed in a later release must not make the whole preferences row unreadable. What the
// list then means (an empty filter set, a missing section) is the domain's call, made by
// LibraryFilterSet and LibrarySections where the row is mapped.
public sealed class EnumListConverter<TEnum>()
    : ValueConverter<IReadOnlyList<TEnum>, string>(
        values => ToText(values),
        text => FromText(text))
    where TEnum : struct, Enum
{
    private const char Separator = ',';

    public static string ToText(IReadOnlyList<TEnum> values) => string.Join(Separator, values);

    public static IReadOnlyList<TEnum> FromText(string text)
    {
        List<TEnum> values = [];

        foreach (var name in text.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Names only: Enum.TryParse would also accept "3" and turn it into a member.
            if (!int.TryParse(name, out _) && Enum.TryParse<TEnum>(name, out var value) && Enum.IsDefined(value))
            {
                values.Add(value);
            }
        }

        return values;
    }
}
