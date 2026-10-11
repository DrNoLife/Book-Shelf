namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// A volume count read from a source's free text, together with where in that text it was
/// read: the provenance the series page explains as "How is this counted?".
/// </summary>
/// <param name="Count">The volumes the text states.</param>
/// <param name="Basis">Which kind of text won: an English publisher's note or the status line.</param>
/// <param name="Evidence">
/// The text the count was read from, verbatim: "Yen Press: 14 Volumes (Ongoing)", or the
/// status line that carried the count.
/// </param>
public sealed record VolumeCountReading(int Count, VolumeCountBasis Basis, string Evidence);
