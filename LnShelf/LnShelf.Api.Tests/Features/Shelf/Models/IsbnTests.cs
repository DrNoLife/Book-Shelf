namespace LnShelf.Api.Tests.Features.Shelf.Models;

/// <summary>
/// What may go into <c>Volume.Isbn</c>. Cheap to test and everything downstream
/// depends on it: an ISBN is the only key per-volume enrichment works by.
/// </summary>
public sealed class IsbnTests
{
    [Theory]
    // The shape the real StoryGraph export writes.
    [InlineData("9781975301170", "9781975301170")]
    [InlineData("978-1-9753-0117-0", "9781975301170")]
    [InlineData("978 1 9753 0117 0", "9781975301170")]

    // ISBN-10, check digit and all.
    [InlineData("031636398X", "031636398X")]
    [InlineData("031636398x", "031636398X")]

    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]

    // Not an ISBN at all: too short, too long, or one of StoryGraph's own ids.
    [InlineData("12345", null)]
    [InlineData("97819753011700000", null)]
    [InlineData("f5a1c0de-0000-4000-8000-000000000000", null)]
    [InlineData("B08XYZ1234", null)]
    public void Normalize_EveryShapeAnExportWrites_IsTheDigitsOrNothing(string? raw, string? expected) =>
        Isbn.Normalize(raw).ShouldBe(expected);

    [Theory]
    // Already 13 digits: only cleaned.
    [InlineData("978-1-59307-020-5", "9781593070205")]

    // The same books as ISBN-10: 978 in front and the check digit recomputed, X included.
    [InlineData("1-59307-020-9", "9781593070205")]
    [InlineData("031636398X", "9780316363983")]

    [InlineData(null, null)]
    [InlineData("not an isbn", null)]
    public void ToIsbn13_EitherForm_IsTheThirteenDigitForm(string? raw, string? expected) =>
        Isbn.ToIsbn13(raw).ShouldBe(expected);

    [Theory]
    // Registration group 4 is Japan, in both the 13- and the 10-digit form.
    [InlineData("9784896374599", true)]
    [InlineData("978-4-89637-459-9", true)]
    [InlineData("4896374599", true)]

    // Yen On, Yen Press, J-Novel Club: group 1, the English market.
    [InlineData("9781975301170", false)]
    [InlineData("9780316363983", false)]
    [InlineData("031636398X", false)]

    [InlineData(null, false)]
    [InlineData("not an isbn", false)]
    public void IsJapaneseEdition_EveryRegistrationGroupTheShelfSees_IsJudgedByItsPrefix(
        string? isbn,
        bool expected) =>
        Isbn.IsJapaneseEdition(isbn).ShouldBe(expected);
}
