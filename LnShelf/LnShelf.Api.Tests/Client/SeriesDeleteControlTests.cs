namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The one destructive control in the app. Asserted against the markup and the scoped
/// stylesheet, which is this project's convention for client behaviour with no browser
/// in the test run.
/// </summary>
// Worth policing rather than leaving to a glance: the class names in the markup and in
// the stylesheet are two separate files that have to agree, and a mismatch here means an
// unstyled delete button rather than a broken build.
public sealed class SeriesDeleteControlTests
{
    private static readonly string MarkupPath =
        Path.Combine("Features", "Series", "Pages", "SeriesPage.razor");

    private static readonly string StylesheetPath =
        Path.Combine("Features", "Series", "Pages", "SeriesPage.razor.css");

    [Fact]
    public void SeriesPage_TheDeleteControl_AsksBeforeItActs()
    {
        var codeBehind = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Pages", "SeriesPage.razor.cs"));

        // The prompt, and the fact that a refusal returns before anything is written.
        codeBehind.ShouldContain("lnshelf.confirm");
        codeBehind.ShouldContain("if (!confirmed || !await SeriesState.DeleteAsync())");

        // And it names the series rather than asking "Are you sure?" about nothing.
        codeBehind.ShouldContain("Remove “{series.TitleEnglish}” from the shelf?");
    }

    [Fact]
    public void SeriesPage_WhatTheDeleteCosts_IsPrintedRatherThanHidden()
    {
        var markup = ClientSource.ReadAllText(MarkupPath);

        // design.md forbids hover-only information, and the cost of this one is the whole
        // reason to hesitate — so it is on the page, not only inside the prompt.
        markup.ShouldContain("danger__fine");
        markup.ShouldContain("@DeleteCost");

        var codeBehind = ClientSource.ReadAllText(
            Path.Combine("Features", "Series", "Pages", "SeriesPage.razor.cs"));
        codeBehind.ShouldContain("This cannot be undone.");
    }

    [Fact]
    public void SeriesPage_TheDeleteButton_GoesInertWhileAWriteIsInFlight()
    {
        // Same rule as every tile: a double-click must not race a request.
        ClientSource.ReadAllText(MarkupPath)
            .ShouldContain("disabled=\"@SeriesState.IsSaving\"");
    }

    [Theory]
    [InlineData(".danger")]
    [InlineData(".danger__fine")]
    public void SeriesPageStylesheet_EveryClassTheMarkupUses_IsStyled(string selector) =>
        ClientSource.ReadAllText(StylesheetPath).ShouldContain(selector);

    [Fact]
    public void SeriesPage_TheDeleteButton_IsTheSharedDestructiveButton()
    {
        // The danger colour belongs to the global .destructive class (app.css), which a
        // scoped stylesheet may not repaint — so the button wears it rather than restyling
        // its own.
        ClientSource.ReadAllText(MarkupPath).ShouldContain("class=\"destructive\"");

        // No raw hex anywhere in this stylesheet — the rule that keeps the palette one file.
        ClientSource.ReadAllText(StylesheetPath).ShouldNotContain("#");
    }

    [Fact]
    public void ClientScript_TheConfirmHelper_Exists()
    {
        // The C# side calls it by name through IJSRuntime, so a rename in one file and not
        // the other is a runtime failure rather than a compile error.
        ClientSource.ReadAllText(Path.Combine("wwwroot", "js", "lnshelf.js"))
            .ShouldContain("confirm: function (message)");
    }
}
