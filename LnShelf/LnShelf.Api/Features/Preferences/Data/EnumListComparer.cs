using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LnShelf.Api.Features.Preferences.Data;

/// <summary>
/// Compares two enum lists by their contents and order, so the change tracker notices a
/// reordered list and keeps a snapshot of its own rather than a reference to the live one.
/// </summary>
// Without it EF compares the list by reference: a list changed in place would never be
// saved, and one replaced by an equal list would be written for nothing.
public sealed class EnumListComparer<TEnum>()
    : ValueComparer<IReadOnlyList<TEnum>>(
        (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
        values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)),
        values => values.ToList())
    where TEnum : struct, Enum;
