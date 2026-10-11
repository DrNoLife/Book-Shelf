namespace LnShelf.Api.Features.Activity.Models;

/// <summary>
/// One shelf event with the series fields "Since your last visit" prints next to it: the
/// shape the query reads and the grouping works on.
/// </summary>
public sealed record ActivityEntry(
    Guid SeriesId,
    string SeriesTitle,
    Format Format,
    ShelfEventKind Kind,
    decimal VolumeNumber,
    DateOnly? PreviousDate,
    DateOnly? NewDate,
    DateTimeOffset OccurredAt);
