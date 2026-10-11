using LnShelf.Client.Features.Import;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The review row's one-tap answers: offered only where the format alone tells the candidates
/// apart, so a format button never picks between two series of one format.
/// </summary>
public sealed class ImportQuickChoicesTests
{
    [Fact]
    public void For_ALightNovelAndItsManga_OffersOneAnswerPerFormatInFormatOrder()
    {
        var group = Group(Candidate(2, Format.Manga), Candidate(1, Format.LightNovel));

        ImportQuickChoices.For(group).ShouldBe(
        [
            new ImportQuickChoice(1, Format.LightNovel),
            new ImportQuickChoice(2, Format.Manga),
        ]);
    }

    [Fact]
    public void For_TwoCandidatesOfOneFormat_OffersNothing() =>
        ImportQuickChoices.For(Group(Candidate(1, Format.LightNovel), Candidate(2, Format.LightNovel), Candidate(3, Format.Manga)))
            .ShouldBeEmpty();

    [Fact]
    public void For_OneCandidate_OffersNothing() =>
        ImportQuickChoices.For(Group(Candidate(1, Format.LightNovel))).ShouldBeEmpty();

    private static ImportGroupResponse Group(params ImportCandidateResponse[] candidates) => new()
    {
        GroupId = Guid.CreateVersion7(),
        SeriesTitle = "The Saga of Tanya the Evil",
        Status = ImportGroupStatus.NeedsReview,
        Reason = ImportReviewReason.FormatAmbiguous,
        RowCount = 3,
        VolumeSummary = "1–3",
        ReadCount = 3,
        TreatReadAsOwned = true,
        Candidates = candidates,
        Rows = [],
    };

    private static ImportCandidateResponse Candidate(int aniListId, Format format) => new()
    {
        AniListId = aniListId,
        TitleEnglish = "The Saga of Tanya the Evil",
        Format = format,
        PublicationStatus = PublicationStatus.Ongoing,
        Score = 100,
    };
}
