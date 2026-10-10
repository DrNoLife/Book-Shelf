using System.Text.Json;

namespace LnShelf.EndToEnd.Tests.Journeys.Auth;

// What each user can reach of another's shelf: nothing. Ids are not secrets, so every
// attempt is made with A's real ids, and each must look exactly like an id that does not
// exist (404), never like one that is forbidden (403), and must change nothing of A's.
public sealed class IsolationTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    [Fact]
    public async Task Library_ANewUser_SeesAnEmptyShelf()
    {
        await using var userB = await Fixture.NewContextAsync(user: TestUser.B);

        var library = await JsonAsync(await userB.APIRequest.GetAsync("api/library?filter=All"));

        library.GetProperty("cards").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task SeriesAndVolumes_AnotherUsersIds_AreNotFoundAndUnchanged()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        var volumeId = await FirstVolumeIdAsync(seriesId);
        var before = await (await Page.APIRequest.GetAsync($"api/series/{seriesId}")).TextAsync();

        await using var userB = await Fixture.NewContextAsync(user: TestUser.B);
        var asB = userB.APIRequest;

        (await asB.GetAsync($"api/series/{seriesId}")).Status.ShouldBe(404);
        (await asB.PutAsync($"api/series/{seriesId}/state", Json(new { trackingStatus = "Dropped" }))).Status.ShouldBe(404);
        (await asB.PutAsync($"api/volumes/{volumeId}/read", Json(new { status = "Unread" }))).Status.ShouldBe(404);
        (await asB.DeleteAsync($"api/series/{seriesId}")).Status.ShouldBe(404);

        var after = await (await Page.APIRequest.GetAsync($"api/series/{seriesId}")).TextAsync();
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Covers_AnotherUsersUpload_IsNotFound()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        var volumeId = await FirstVolumeIdAsync(seriesId);
        var upload = await Page.APIRequest.PutAsync($"api/volumes/{volumeId}/cover", new()
        {
            Multipart = Page.APIRequest.CreateFormData().Append("file", new FilePayload
            {
                Name = "cover.png",
                MimeType = "image/png",
                Buffer = await File.ReadAllBytesAsync(StubImageHandler.ImagePath),
            }),
        });
        upload.Ok.ShouldBeTrue($"The upload answered {upload.Status}.");
        var coverKey = Fixture.CoverStore.ObjectKeys.Single(key => key.StartsWith("override/", StringComparison.Ordinal));

        (await Page.APIRequest.GetAsync($"api/covers/{coverKey}")).Status.ShouldBe(200);

        await using var userB = await Fixture.NewContextAsync(user: TestUser.B);
        (await userB.APIRequest.GetAsync($"api/covers/{coverKey}")).Status.ShouldBe(404);
    }

    [Fact]
    public async Task DeleteAccount_RemovesThatUserOnly()
    {
        await using var userB = await Fixture.NewContextAsync(user: TestUser.B);

        (await userB.APIRequest.DeleteAsync("api/auth/account")).Status.ShouldBe(204);
        (await userB.APIRequest.GetAsync("api/library")).Status.ShouldBe(401);

        var library = await JsonAsync(await Page.APIRequest.GetAsync("api/library?filter=All"));
        library.GetProperty("cards").GetArrayLength().ShouldBeGreaterThan(0);
    }

    private async Task<Guid> FirstVolumeIdAsync(Guid seriesId)
    {
        var detail = await JsonAsync(await Page.APIRequest.GetAsync($"api/series/{seriesId}"));
        return detail.GetProperty("volumes")[0].GetProperty("volumeId").GetGuid();
    }

    private static APIRequestContextOptions Json(object body) => new() { DataObject = body };

    private static async Task<JsonElement> JsonAsync(IAPIResponse response)
    {
        response.Ok.ShouldBeTrue($"{response.Url} answered {response.Status}.");
        return JsonDocument.Parse(await response.TextAsync()).RootElement;
    }
}
