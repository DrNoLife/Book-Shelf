namespace LnShelf.Api.Features.Calendar.Models;

/// <summary>
/// One dated volume, as the release calendar shows it: an all-day event on its release date.
/// </summary>
/// <param name="VolumeId">Makes the event's UID, so a calendar app updates a moved date in place.</param>
/// <param name="Format">Tells the light novel and the manga of one title apart in the summary.</param>
public sealed record CalendarEntry(Guid VolumeId, string SeriesTitle, Format Format, decimal VolumeNumber, DateOnly ReleaseDate);
