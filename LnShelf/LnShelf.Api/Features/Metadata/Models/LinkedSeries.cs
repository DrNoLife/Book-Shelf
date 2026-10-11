namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>A series on the shelf that already holds a release-source record.</summary>
public sealed record LinkedSeries(Guid SeriesId, long MangaUpdatesId, string Title);
