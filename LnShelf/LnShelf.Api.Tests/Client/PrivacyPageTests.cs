using System.Text.RegularExpressions;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The privacy page is a static file the Google consent screen links to, served under a strict
/// CSP, so what it must carry and what it must not are checked against the source.
/// </summary>
public sealed partial class PrivacyPageTests
{
    private static string Page => ClientSource.ReadAllText(Path.Combine("wwwroot", "privacy.html"));

    // Article 13: who is responsible and how to reach them, the bases, recipients, transfers,
    // retention, the rights and the right to complain. A missing one is a missing duty.
    [Theory]
    [InlineData("iamdrnolife@gmail.com")]
    [InlineData("Article 6(1)(b)")]
    [InlineData("Article 6(1)(f)")]
    [InlineData("Cloudflare")]
    [InlineData("Google")]
    [InlineData("EU-US Data Privacy Framework")]
    [InlineData("How long we keep it")]
    [InlineData("data portability")]
    [InlineData("Export my data")]
    [InlineData("Datatilsynet")]
    [InlineData("within one month")]
    public void Page_CarriesWhatArticle13Asks(string expected) => Page.ShouldContain(expected);

    [Fact]
    public void Page_NamesTheHiddenSuggestions()
    {
        Page.ShouldContain("Not interested");
        Page.ShouldContain("Show again");
    }

    // The CSP allows no inline script, so an inline one would simply not run, and the page
    // would be broken without anything saying so.
    [Fact]
    public void Page_HasNoInlineScriptOrEventHandler()
    {
        var scripts = ScriptTag().Matches(Page).Select(match => match.Value).ToList();

        scripts.ShouldBe(["<script src=\"js/privacy.js\"></script>"]);
        Regex.IsMatch(Page, @"\son[a-z]+\s*=").ShouldBeFalse("an inline event handler is blocked by the CSP");
    }

    // The statement the page makes about the browser, kept true: a font, script or image from
    // another origin would also be refused by the CSP.
    [Fact]
    public void Page_LoadsNothingFromAnotherOrigin()
    {
        var loaded = Regex.Matches(Page, @"\b(?:src|href)=""(https?:)?//[^""]*""")
            .Select(match => match.Value)
            .Where(value => !value.StartsWith("href=\"https://www.datatilsynet.dk", StringComparison.Ordinal))
            .ToList();

        loaded.ShouldBeEmpty();
        Page.ShouldNotContain("@import");
    }

    [Fact]
    public void Page_EveryContentsLinkHasAHeadingToLandOn()
    {
        var targets = Regex.Matches(Page, @"<li><a href=""#([a-z]+)"">").Select(match => match.Groups[1].Value).ToList();
        var ids = Regex.Matches(Page, @"\bid=""([a-z-]+)""").Select(match => match.Groups[1].Value).ToList();

        targets.Count.ShouldBeGreaterThan(5);
        ids.Count.ShouldBe(ids.Distinct().Count(), "two elements share an id, so a contents link lands on the wrong one");
        foreach (var target in targets)
        {
            ids.ShouldContain(target, $"the contents list links to #{target}, which nothing on the page is called");
            ids.ShouldContain($"{target}-title");
        }
    }

    [Fact]
    public void Page_UsesTheWelcomeLookAndWorksWithoutASession()
    {
        Page.ShouldContain("css/welcome.css");
        Page.ShouldContain("css/privacy.css");
        Page.ShouldContain("data-session-link href=\"api/auth/login?returnUrl=%2F\"");

        var script = ClientSource.ReadAllText(Path.Combine("wwwroot", "js", "privacy.js"));
        script.ShouldContain("api/auth/me");
        script.ShouldContain("Back to your shelf");
    }

    [Fact]
    public void Stylesheet_CarriesNoColourOfItsOwn()
    {
        var css = Regex.Replace(
            ClientSource.ReadAllText(Path.Combine("wwwroot", "css", "privacy.css")), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        Regex.Matches(css, @"#[0-9a-fA-F]{3,8}\b|\brgba?\(").ShouldBeEmpty("design.md fixes the palette; use the tokens");
    }

    // The links that already pointed at privacy.html keep working: the page did not move.
    [Theory]
    [InlineData("wwwroot/welcome.html")]
    [InlineData("wwwroot/signed-out.html")]
    [InlineData("Layout/Sidebar.razor")]
    [InlineData("Layout/PublicLayout.razor")]
    [InlineData("Features/Profile/Components/AccountCard.razor")]
    public void EverySurface_LinksToThePrivacyPage(string relativePath) =>
        ClientSource.ReadAllText(relativePath.Replace('/', Path.DirectorySeparatorChar)).ShouldContain("href=\"privacy.html\"");

    // A plain link, so it needs no script and the CSP has nothing to say about it.
    [Fact]
    public void AccountCard_OffersTheExportAsADownloadLink()
    {
        var card = ClientSource.ReadAllText(Path.Combine("Features", "Profile", "Components", "AccountCard.razor"));

        card.ShouldContain("href=\"api/auth/account/export\" download");
        card.ShouldContain("Export my data");
    }

    [GeneratedRegex(@"<script\b[^>]*>.*?</script>", RegexOptions.Singleline)]
    private static partial Regex ScriptTag();
}
