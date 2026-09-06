namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Whether two credits name the same person. It decides a format tie-break, so
/// the failure that matters is the quiet one: a "no" for two spellings of one
/// name leaves the tie unbroken and nothing says why.
/// </summary>
public sealed class PersonNameNormalizerTests
{
    [Theory]
    [InlineData("Shinobu Shinotsuki", "shinobu shinotsuki")]
    [InlineData("so-bin", "so bin")]
    [InlineData("  Sirotaka  ", "sirotaka")]
    [InlineData("Rifujin na Magonote", "rifujin na magonote")]

    // Diacritics decompose and the marks go, so the romanisation that kept them
    // and the one that dropped them are one name.
    [InlineData("Kōhei Horikoshi", "kohei horikoshi")]
    [InlineData("Melanie Höpfler", "melanie hopfler")]

    // Punctuation is a separator, not a letter.
    [InlineData("Zen, Carlo", "zen carlo")]
    [InlineData("K. Ayasato", "k ayasato")]

    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(",.-", "")]
    public void Normalize_ANameAsASourceWroteIt_IsReducedToItsComparableForm(string? name, string expected) =>
        PersonNameNormalizer.Normalize(name).ShouldBe(expected);

    [Theory]
    [InlineData("Shinobu Shinotsuki", "Shinobu Shinotsuki")]
    [InlineData("Shinobu Shinotsuki", "shinobu  shinotsuki")]

    // The order the parts were written in is the source's convention, not the
    // person's identity — and no source says which convention it used.
    [InlineData("Shinobu Shinotsuki", "Shinotsuki, Shinobu")]
    [InlineData("Carlo Zen", "Zen, Carlo")]

    [InlineData("Kōhei Horikoshi", "Kohei Horikoshi")]
    [InlineData("so-bin", "so bin")]
    public void AreSamePerson_TwoSpellingsOfOneName_IsTrue(string left, string right)
    {
        PersonNameNormalizer.AreSamePerson(left, right).ShouldBeTrue();

        // Symmetric, or the tie-break would depend on which source was asked first.
        PersonNameNormalizer.AreSamePerson(right, left).ShouldBeTrue();
    }

    [Theory]
    [InlineData("so-bin", "Hugin Miyama")]
    [InlineData("Shinobu Shinotsuki", "Chika Tojo")]

    // A shared surname is not the same person.
    [InlineData("Shinobu Shinotsuki", "Yuu Shinotsuki")]

    // A missing name never matches — not even another missing one. Two unknowns
    // are not evidence that a candidate is the right one.
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("so-bin", null)]
    [InlineData(null, "so-bin")]
    public void AreSamePerson_TwoDifferentOrMissingNames_IsFalse(string? left, string? right)
    {
        PersonNameNormalizer.AreSamePerson(left, right).ShouldBeFalse();
        PersonNameNormalizer.AreSamePerson(right, left).ShouldBeFalse();
    }
}
