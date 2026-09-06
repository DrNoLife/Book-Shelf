namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The one verification a guessed ISBN gets, shared by the nightly search fallback and the
/// discovery command. Both used to own a copy, and the copies disagreed.
/// </summary>
public sealed class IsbnCandidateVerifierTests
{
    private const string SeriesTitle = "Overlord";
    private const int VolumeNumber = 3;

    private const string EnglishIsbn = "9780316363921";
    private const string OtherEnglishIsbn = "9780316363938";

    /// <summary>A 978-4 ISBN: the Japanese registration group.</summary>
    private const string JapaneseIsbn = "9784041030721";

    [Fact]
    public async Task VerifyAsync_ARecordNamingEnglishAndTheMedium_SaysBoth()
    {
        var verified = await Verifier(Edition("Overlord, Vol. 3 (light Novel)", EditionLanguage.English))
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, EnglishIsbn, CancellationToken.None);

        verified.ShouldNotBeNull();
        verified.Isbn.ShouldBe(EnglishIsbn);
        verified.NamesEnglish.ShouldBeTrue();
        verified.ConfirmsMedium.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyAsync_ARecordSilentAboutTheMedium_SaysSoRatherThanRefusing()
    {
        var verified = await Verifier(Edition("Overlord 3", EditionLanguage.English))
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, EnglishIsbn, CancellationToken.None);

        // The write path refuses this and the review path offers it with the doubt spelled
        // out, which is only possible because the verifier reports rather than decides.
        verified.ShouldNotBeNull();
        verified.NamesEnglish.ShouldBeTrue();
        verified.ConfirmsMedium.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyAsync_ARecordStatingNoLanguage_IsNotTreatedAsADenial()
    {
        var verified = await Verifier(Edition("Overlord, Vol. 3 (light Novel)", EditionLanguage.Unstated))
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, EnglishIsbn, CancellationToken.None);

        // Unstated is the common English case. Collapsing it into "not English" would throw
        // away real editions, so it comes back with NamesEnglish false and nothing else said.
        verified.ShouldNotBeNull();
        verified.NamesEnglish.ShouldBeFalse();
        verified.ConfirmsMedium.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Overlord, Vol. 4 (light Novel)")]
    [InlineData("Overlord's Unfinished Business, Vol. 3")]
    [InlineData("That Time I Got Reincarnated as a Slime, Vol. 3 (light Novel)")]
    public async Task VerifyAsync_ARecordThatIsNotThisSeriesVolume_ProvesNothing(string recordTitle)
    {
        var verified = await Verifier(Edition(recordTitle, EditionLanguage.English))
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, EnglishIsbn, CancellationToken.None);

        verified.ShouldBeNull();
    }

    [Fact]
    public async Task VerifyAsync_ARecordNamingAnotherLanguage_ProvesNothing()
    {
        var verified = await Verifier(Edition("Overlord, Vol. 3 (light Novel)", EditionLanguage.Other))
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, EnglishIsbn, CancellationToken.None);

        verified.ShouldBeNull();
    }

    [Fact]
    public async Task VerifyAsync_AnIsbnTheSourceHasNeverIndexed_ProvesNothing()
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((VolumeEnrichment?)null);

        var verified = await new IsbnCandidateVerifier(source)
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, EnglishIsbn, CancellationToken.None);

        verified.ShouldBeNull();
    }

    [Fact]
    public async Task VerifyAsync_AJapaneseRegisteredIsbn_IsRefusedWithoutASingleRequest()
    {
        // The divergence this class settled. The write path always refused these before
        // spending a lookup; the review path used to fetch them and refuse them only when
        // the record stated no language — so a Japanese ISBN whose record happened to say
        // English could be proposed, and applying it would leave the volume permanently
        // unenrichable, because the nightly pass refuses to look a Japanese ISBN up.
        var source = EnrichmentSource(JapaneseIsbn, Edition("Overlord, Vol. 3 (light Novel)", EditionLanguage.English));

        var verified = await new IsbnCandidateVerifier(source)
            .VerifyAsync(SeriesTitle, VolumeNumber, Format.LightNovel, JapaneseIsbn, CancellationToken.None);

        verified.ShouldBeNull();
        await source.DidNotReceive().GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EnglishIsbn, null, true)]
    [InlineData(EnglishIsbn, OtherEnglishIsbn, true)]

    // Already asked about directly, which is what got the search started.
    [InlineData(EnglishIsbn, EnglishIsbn, false)]

    // A Japanese-market edition: the Japanese date and the Japanese cover, neither of which
    // belongs on an English shelf entry. Judged from the registration group, which is free.
    [InlineData(JapaneseIsbn, null, false)]
    [InlineData(JapaneseIsbn, EnglishIsbn, false)]
    public void IsWorthAsking_ACandidate_IsJudgedFromTheIsbnAlone(
        string candidateIsbn, string? heldIsbn, bool expected) =>
        IsbnCandidateVerifier.IsWorthAsking(candidateIsbn, heldIsbn).ShouldBe(expected);

    private static IsbnCandidateVerifier Verifier(VolumeEnrichment edition) =>
        new(EnrichmentSource(EnglishIsbn, edition));

    private static IVolumeEnrichmentSource EnrichmentSource(string isbn, VolumeEnrichment edition)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((VolumeEnrichment?)null);
        source.GetByIsbnAsync(isbn, Arg.Any<CancellationToken>()).Returns(edition);

        return source;
    }

    private static VolumeEnrichment Edition(string title, EditionLanguage language) => new()
    {
        SourceRecordKey = "/books/OL1M",
        Title = title,
        Publisher = "Yen On",
        Language = language,
    };
}
