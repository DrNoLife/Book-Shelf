namespace LnShelf.Client.Features.Stats.Components;

/// <summary>One month's column in a <see cref="MonthBarChart"/>.</summary>
/// <param name="Month">The first day of the month.</param>
/// <param name="Value">What the column's height is scaled from.</param>
/// <param name="ValueLabel">Printed above the column, so the figure is never hover-only.</param>
/// <param name="Words">What the value counts, for the column's accessible name: "9 volumes".</param>
/// <param name="IsPartial">The current month: still filling, so it is drawn hatched and named "so far".</param>
public sealed record MonthBar(DateOnly Month, decimal Value, string ValueLabel, string Words, bool IsPartial);
