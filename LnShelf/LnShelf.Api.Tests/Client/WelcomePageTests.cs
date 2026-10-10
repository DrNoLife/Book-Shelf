namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The signed-out landing page is a static file the client only reaches by name, so the
/// redirect and the page are checked against each other.
/// </summary>
public sealed class WelcomePageTests
{
    [Fact]
    public void SignInRedirect_SendsTheBrowserToTheWelcomePage_NotStraightToTheProvider()
    {
        var handler = ClientSource.ReadAllText("SignInRedirectHandler.cs");

        handler.ShouldContain("welcome.html?returnUrl=");
        handler.ShouldNotContain("NavigateTo($\"api/auth/login");
    }

    [Fact]
    public void WelcomePage_EverySignInLinkGoesToTheLoginEndpoint()
    {
        var page = ClientSource.ReadAllText(Path.Combine("wwwroot", "welcome.html"));

        page.ShouldContain("data-sign-in href=\"api/auth/login?returnUrl=%2F\"");
        page.ShouldContain("js/welcome.js");
    }

    [Fact]
    public void WelcomeScript_PassesOnLocalReturnUrlsOnly()
    {
        var script = ClientSource.ReadAllText(Path.Combine("wwwroot", "js", "welcome.js"));

        script.ShouldContain("api/auth/login?returnUrl=");
        script.ShouldContain("charAt(1) === \"/\"");
    }
}
