namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Looks one candidate ISBN up and reports what its own record turned out to say about
/// this series' volume N.
/// </summary>
// The one place a guessed ISBN is checked. Two callers need it — the nightly search
// fallback, which believes a candidate and writes from it, and the discovery command, which
// proposes one into a file for a person to read — and they had grown their own copies of
// the same four steps: look the ISBN up, refuse a record naming another language, refuse a
// record that is not this series' volume N, then ask whether it states the medium.
//
// The copies had already drifted, which is why they are one class now (see IsWorthAsking).
// What each caller does with the answer is still its own business: the write path takes the
// first record that states the medium, and the review path grades every candidate and
// offers the best of them with the doubt spelled out.
public sealed class IsbnCandidateVerifier(IVolumeEnrichmentSource enrichmentSource)
{
    /// <summary>
    /// Whether a candidate is worth a request at all — judged from the ISBN alone, so it
    /// costs nothing.
    /// </summary>
    /// <param name="heldIsbn">
    /// The ISBN already on the volume, where there is one. It was asked about directly
    /// before any of this, and asking again would spend a request on a known answer.
    /// </param>
    // The Japanese rule is the divergence this class settled. A 978-4 ISBN is registered in
    // the Japanese group, so it resolves to a Japanese-market edition — the Japanese date,
    // the Japanese cover — and neither belongs on an English shelf entry. The write path
    // always refused those before spending a lookup; the review path used to fetch them and
    // refuse them only when the record stated no language, so a Japanese ISBN whose record
    // happened to say English could be proposed. It cannot any more, and that is the
    // stricter side winning for a reason beyond caution: what discovery proposes is written
    // into Volume.Isbn, and VolumeGaps.WorthAskingByIsbn refuses to look a Japanese ISBN up,
    // so applying one would leave that volume permanently unenrichable.
    public static bool IsWorthAsking(string candidateIsbn, string? heldIsbn) =>
        !string.Equals(candidateIsbn, heldIsbn, StringComparison.Ordinal)
        && !Isbn.IsJapaneseEdition(candidateIsbn);

    /// <returns>
    /// Null when the candidate proves nothing: not indexed, a record in another language, or
    /// a record that is not this series' volume N.
    /// </returns>
    public async Task<IsbnCandidateVerification?> VerifyAsync(
        string seriesTitle,
        int volumeNumber,
        Format format,
        string candidateIsbn,
        CancellationToken cancellationToken)
    {
        if (!IsWorthAsking(candidateIsbn, heldIsbn: null))
        {
            return null;
        }

        var record = await enrichmentSource.GetByIsbnAsync(candidateIsbn, cancellationToken);

        if (record is null || record.Language is EditionLanguage.Other)
        {
            return null;
        }

        // The record has to name this series, this volume number and the right medium. The
        // searches that feed this are deliberately loose — they have to be, or they find
        // nothing — so this is what keeps a manga volume out of a light novel's row, and a
        // neighbouring volume out of this one's.
        if (!VolumeEditionMatcher.Matches(seriesTitle, volumeNumber, format, record))
        {
            return null;
        }

        return new IsbnCandidateVerification
        {
            Isbn = candidateIsbn,
            Record = record,
            NamesEnglish = record.Language is EditionLanguage.English,
            ConfirmsMedium = VolumeEditionMatcher.ConfirmsMedium(format, record),
        };
    }
}

/// <summary>A candidate ISBN that really is this series' volume N, and what its record says.</summary>
public sealed record IsbnCandidateVerification
{
    public required string Isbn { get; init; }

    public required VolumeEnrichment Record { get; init; }

    /// <summary>
    /// Whether the record states English outright. False means it states no language at all,
    /// which is the common English case rather than a denial — refusing every one of those
    /// would throw away real editions.
    /// </summary>
    public required bool NamesEnglish { get; init; }

    /// <summary>
    /// Whether the record says which medium it is, rather than merely failing to say it is
    /// the other one.
    /// </summary>
    public required bool ConfirmsMedium { get; init; }
}
