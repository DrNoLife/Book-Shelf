namespace LnShelf.EndToEnd.Tests.Journeys.Auth;

// Not a journey through the UI: what the API does for a browser with no session, which is
// the one thing every other journey's sign-in hides.
public sealed class AuthenticationTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    [Fact]
    public async Task Api_NoSession_AnswersUnauthorizedWithProblemDetails()
    {
        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);

        var response = await anonymous.APIRequest.GetAsync("api/library");

        response.Status.ShouldBe(401);
        response.Headers["content-type"].ShouldStartWith("application/problem+json");
    }

    [Fact]
    public async Task Api_AfterLogout_AnswersUnauthorized()
    {
        (await Page.APIRequest.GetAsync("api/library")).Status.ShouldBe(200);

        (await Page.APIRequest.PostAsync("api/auth/logout")).Status.ShouldBe(204);

        (await Page.APIRequest.GetAsync("api/library")).Status.ShouldBe(401);
    }

    [Fact]
    public async Task Health_NoSession_IsOpen()
    {
        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);

        (await anonymous.APIRequest.GetAsync("api/health")).Status.ShouldBe(200);
    }

    [Fact]
    public async Task Sidebar_SignOut_EndsTheSessionAndLeavesTheApp()
    {
        await Page.GotoAsync("/");
        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        await Page.WaitForURLAsync("**/signed-out.html");
        (await Page.APIRequest.GetAsync("api/library")).Status.ShouldBe(401);
    }

    // The provider is never contacted in tests, so the login request is answered here: what
    // matters is that a browser with no session is sent to it, with the page to come back to.
    [Fact]
    public async Task AnyPage_NoSession_SendsTheBrowserToSignInAndBack()
    {
        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);
        var page = await anonymous.NewPageAsync();
        await page.RouteAsync("**/api/auth/login**", route => route.FulfillAsync(new() { Status = 200, Body = "login" }));

        await page.GotoAsync("/upcoming");

        await page.WaitForURLAsync("**/api/auth/login?returnUrl=%2Fupcoming");
    }
}
