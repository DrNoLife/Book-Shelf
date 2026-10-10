using System.Text.Json;

namespace LnShelf.EndToEnd.Tests.Journeys.Sharing;

// The read-only shelf by link (feature-spec §4.8), from the visitor's side: a browser with no
// session opens the link and sees the owner's shelf without ever being sent to sign in, and a
// dead link is a 404 that says so in place.
//
// The link is made through the API rather than by clicking ShelfSharingCard: the card's host
// page (the user page) is built separately. Once it is, the first journey can turn the shelf
// public through the card's role-based controls: group "Who can see your shelf", buttons
// "Private" and "Public by link", "Copy link", "New link".
public sealed class SharedShelfTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string ReadingTitle = "The Saga of Tanya the Evil";
    private const string DroppedTitle = "Classroom of the Elite";

    [Fact]
    public async Task SharedShelf_OwnerMakesItPublic_AnonymousVisitorSeesTheShelfWithoutSigningIn()
    {
        var droppedId = await ShelfLookup.SeriesIdAsync(Page, DroppedTitle);
        (await Page.APIRequest.PutAsync($"api/series/{droppedId}/state", Json(new { trackingStatus = "Dropped" })))
            .Ok.ShouldBeTrue();
        var token = await MakePublicAsync(Page.APIRequest);

        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);
        var visitor = await anonymous.NewPageAsync();
        await visitor.GotoAsync($"/shared/{token}");

        await Assertions.Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = "A shelf, shared with you" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = ReadingTitle, Exact = true }))
            .ToBeVisibleAsync();

        visitor.Url.ShouldContain($"/shared/{token}");
        await Assertions.Expect(visitor.GetByText(DroppedTitle, new() { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(visitor.GetByText("To buy")).ToHaveCountAsync(0);
        await Assertions.Expect(visitor.GetByText("owned", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SharedShelf_RegenerateLink_OldLinkShowsNotShared()
    {
        var oldToken = await MakePublicAsync(Page.APIRequest);
        var regenerated = await JsonAsync(await Page.APIRequest.PostAsync("api/sharing/regenerate"));
        var newToken = regenerated.GetProperty("token").GetString().ShouldNotBeNull();

        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);

        (await anonymous.APIRequest.GetAsync($"api/shared/{oldToken}")).Status.ShouldBe(404);
        (await anonymous.APIRequest.GetAsync($"api/shared/{newToken}")).Status.ShouldBe(200);

        var visitor = await anonymous.NewPageAsync();
        await visitor.GotoAsync($"/shared/{oldToken}");

        await Assertions.Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = "This shelf isn't shared" }))
            .ToBeVisibleAsync();
        visitor.Url.ShouldContain($"/shared/{oldToken}");
    }

    [Fact]
    public async Task SharedShelf_MadePrivate_LinkIsNotFound()
    {
        var token = await MakePublicAsync(Page.APIRequest);

        var privateAgain = await JsonAsync(await Page.APIRequest.PutAsync("api/sharing", Json(new { isPublic = false })));
        privateAgain.GetProperty("isPublic").GetBoolean().ShouldBeFalse();

        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);
        var response = await anonymous.APIRequest.GetAsync($"api/shared/{token}");

        response.Status.ShouldBe(404);
        response.Headers["content-type"].ShouldStartWith("application/problem+json");
    }

    // B's series art through A's token is a 404, even though the key is real and B's own link
    // serves it. And the owner's setting itself is never anonymous.
    [Fact]
    public async Task SharedShelf_UserAsToken_NeverServesUserBsCovers()
    {
        var tokenA = await MakePublicAsync(Page.APIRequest);

        await using var userB = await Fixture.NewContextAsync(user: TestUser.B);
        var added = await userB.APIRequest.PostAsync("api/series", Json(new
        {
            aniListId = FixtureSeriesMetadataSource.MushokuTenseiNovelId,
            format = "LightNovel",
        }));
        added.Ok.ShouldBeTrue($"Adding the series as B answered {added.Status}.");
        var tokenB = await MakePublicAsync(userB.APIRequest);

        var shelfB = await JsonAsync(await userB.APIRequest.GetAsync($"api/shared/{tokenB}"));
        var coverKeyB = shelfB.GetProperty("series")[0].GetProperty("coverObjectKey").GetString()
            .ShouldNotBeNull("Adding the series should have cached its AniList cover (FixtureSeriesMetadataSource).");

        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);
        var asVisitor = anonymous.APIRequest;

        (await asVisitor.GetAsync($"api/shared/{tokenB}/covers/{coverKeyB}")).Status.ShouldBe(200);
        (await asVisitor.GetAsync($"api/shared/{tokenA}/covers/{coverKeyB}")).Status.ShouldBe(404);
        (await asVisitor.GetAsync("api/sharing")).Status.ShouldBe(401);
    }

    private static async Task<string> MakePublicAsync(IAPIRequestContext request)
    {
        var sharing = await JsonAsync(await request.PutAsync("api/sharing", Json(new { isPublic = true })));

        sharing.GetProperty("isPublic").GetBoolean().ShouldBeTrue();
        return sharing.GetProperty("token").GetString().ShouldNotBeNull();
    }

    private static APIRequestContextOptions Json(object body) => new() { DataObject = body };

    private static async Task<JsonElement> JsonAsync(IAPIResponse response)
    {
        response.Ok.ShouldBeTrue($"{response.Url} answered {response.Status}.");
        return JsonDocument.Parse(await response.TextAsync()).RootElement;
    }
}
