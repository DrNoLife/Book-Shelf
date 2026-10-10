using System.Net;
using LnShelf.Client;
using Microsoft.AspNetCore.Components;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// A visitor without an account is never sent to the login from a shared shelf (feature-spec §4.8).
/// </summary>
// The page is safe only while nothing on it calls a signed-in endpoint: the sidebar's sync
// state alone would answer 401 and SignInRedirectHandler would bounce a stranger to Google.
// Asserted on the source, the project's convention for client behaviour without a browser.
public sealed class SharedShelfPageTests
{
    private static readonly string[] SignedInPieces =
        ["<Sidebar", "SyncState", "LibraryState", "PreferencesState", "ImportState", "ShelfSharingState"];

    [Theory]
    [InlineData("Layout/PublicLayout.razor")]
    [InlineData("Features/Sharing/Pages/SharedShelfPage.razor")]
    [InlineData("Features/Sharing/Pages/SharedShelfPage.razor.cs")]
    [InlineData("Features/Sharing/Components/SharedSeriesCard.razor")]
    public void PublicLayoutAndSharedShelfPage_InjectNoSignedInState(string relativePath)
    {
        var source = ClientSource.ReadAllText(relativePath);

        foreach (var piece in SignedInPieces)
        {
            source.ShouldNotContain(piece, Case.Sensitive, $"{relativePath} reaches signed-in state through {piece}.");
        }
    }

    [Fact]
    public void SharedShelfPage_UsesPublicLayout() =>
        ClientSource.ReadAllText("Features/Sharing/Pages/SharedShelfPage.razor").ShouldContain("@layout PublicLayout");

    [Theory]
    [InlineData("shared/AAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("upcoming", true)]
    public async Task SignInRedirectHandler_OnASharedShelf_DoesNotRedirect(string page, bool expectsLogin)
    {
        var navigation = new RecordingNavigationManager($"https://shelf.test/{page}");
        using var invoker = new HttpMessageInvoker(
            new SignInRedirectHandler(navigation) { InnerHandler = new UnauthorizedHandler() });

        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://shelf.test/api/shared/AAAAAAAAAAAAAAAAAAAAAA"),
            CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        if (expectsLogin)
        {
            navigation.NavigatedTo.ShouldNotBeNull().ShouldStartWith("welcome.html");
        }
        else
        {
            navigation.NavigatedTo.ShouldBeNull();
        }
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager(string uri) => Initialize("https://shelf.test/", uri);

        public string? NavigatedTo { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options) => NavigatedTo = uri;
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }
}
