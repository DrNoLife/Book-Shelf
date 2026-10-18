using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.EndToEnd.Tests.Journeys.Covers;

// One user's uploaded cover reaches another who tracks the same catalogued volume, only after
// the first has switched sharing on, and outlives the first's shelf once the second picked it.
// API-level like the isolation journeys: real DI, real Postgres, a fake store behind the seam.
public sealed class CommunityCoverTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    [Fact]
    public async Task CommunityCover_SharedByA_IsPickedByB_AndOutlivesA()
    {
        var request = Page.APIRequest;
        var seriesIdA = await AddSeriesAsync(request);
        var (volumeIdA, number) = await FirstVolumeAsync(request, seriesIdA);

        await UploadCoverAsync(request, volumeIdA);
        var sharing = await JsonAsync(await request.PutAsync("api/community-covers/sharing", Json(new { enabled = true })));
        sharing.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        sharing.GetProperty("sharedCount").GetInt32().ShouldBe(1);

        await using var userB = await Fixture.NewContextAsync(user: TestUser.B);
        var asB = userB.APIRequest;
        var seriesIdB = await AddSeriesAsync(asB);
        var volumeIdB = await VolumeIdAsync(asB, seriesIdB, number);

        var candidates = (await JsonAsync(await asB.GetAsync($"api/volumes/{volumeIdB}/community-covers")))
            .GetProperty("candidates");
        candidates.GetArrayLength().ShouldBe(1);
        var candidate = candidates[0];
        candidate.GetProperty("sharedBy").GetArrayLength().ShouldBe(1);
        candidate.GetProperty("sharedBy")[0].GetString().ShouldNotBeNullOrWhiteSpace();
        candidate.GetProperty("isCurrent").GetBoolean().ShouldBeFalse();
        var contentHash = candidate.GetProperty("contentHash").GetString().ShouldNotBeNull();

        // Signed-in users only.
        (await asB.GetAsync($"api/community-covers/{contentHash}")).Status.ShouldBe(200);
        await using var anonymous = await Fixture.NewContextAsync(signedIn: false);
        (await anonymous.APIRequest.GetAsync($"api/community-covers/{contentHash}")).Status.ShouldBe(401);

        // B takes it: an ordinary override of B's own from then on.
        var picked = await JsonAsync(await asB.PutAsync(
            $"api/volumes/{volumeIdB}/cover/community", Json(new { contentHash })));
        var pickedVolume = VolumeOf(picked, volumeIdB);
        pickedVolume.GetProperty("hasCoverOverride").GetBoolean().ShouldBeTrue();
        var coverKeyB = pickedVolume.GetProperty("coverObjectKey").GetString().ShouldNotBeNull();
        (await asB.GetAsync($"api/covers/{coverKeyB}")).Status.ShouldBe(200);

        // A leaves the shelf: nothing is offered any more, but B's copy is B's.
        (await request.DeleteAsync($"api/series/{seriesIdA}")).Status.ShouldBe(204);

        var after = await JsonAsync(await asB.GetAsync($"api/volumes/{volumeIdB}/community-covers"));
        after.GetProperty("candidates").GetArrayLength().ShouldBe(0);
        (await asB.GetAsync($"api/covers/{coverKeyB}")).Status.ShouldBe(200);
    }

    [Fact]
    public async Task CommunityCover_WhenTheSharerDeletesTheirAccount_LeavesNoRows()
    {
        var request = Page.APIRequest;
        var seriesIdA = await AddSeriesAsync(request);
        var (volumeIdA, _) = await FirstVolumeAsync(request, seriesIdA);

        await UploadCoverAsync(request, volumeIdA);
        (await request.PutAsync("api/community-covers/sharing", Json(new { enabled = true }))).Ok.ShouldBeTrue();
        (await CountRowsAsync()).ShouldBe(1);

        (await request.DeleteAsync("api/auth/account")).Status.ShouldBe(204);

        (await CountRowsAsync()).ShouldBe(0);
    }

    private async Task<int> CountRowsAsync()
    {
        var count = 0;
        await Fixture.SeedAsync(async context => count = await context.Set<CommunityCover>().CountAsync());
        return count;
    }

    private static async Task<Guid> AddSeriesAsync(IAPIRequestContext request)
    {
        var added = await JsonAsync(await request.PostAsync("api/series", Json(new
        {
            aniListId = FixtureSeriesMetadataSource.MushokuTenseiNovelId,
            format = "LightNovel",
        })));

        return added.GetProperty("seriesId").GetGuid();
    }

    private static async Task<(Guid VolumeId, decimal Number)> FirstVolumeAsync(IAPIRequestContext request, Guid seriesId)
    {
        var detail = await JsonAsync(await request.GetAsync($"api/series/{seriesId}"));
        var volume = detail.GetProperty("volumes")[0];
        return (volume.GetProperty("volumeId").GetGuid(), volume.GetProperty("number").GetDecimal());
    }

    private static async Task<Guid> VolumeIdAsync(IAPIRequestContext request, Guid seriesId, decimal number)
    {
        var detail = await JsonAsync(await request.GetAsync($"api/series/{seriesId}"));
        return detail.GetProperty("volumes").EnumerateArray()
            .Single(volume => volume.GetProperty("number").GetDecimal() == number)
            .GetProperty("volumeId").GetGuid();
    }

    private static JsonElement VolumeOf(JsonElement detail, Guid volumeId) =>
        detail.GetProperty("volumes").EnumerateArray()
            .Single(volume => volume.GetProperty("volumeId").GetGuid() == volumeId);

    private static async Task UploadCoverAsync(IAPIRequestContext request, Guid volumeId)
    {
        var upload = await request.PutAsync($"api/volumes/{volumeId}/cover", new()
        {
            Multipart = request.CreateFormData().Append("file", new FilePayload
            {
                Name = "cover.png",
                MimeType = "image/png",
                Buffer = await File.ReadAllBytesAsync(StubImageHandler.ImagePath),
            }),
        });
        upload.Ok.ShouldBeTrue($"The upload answered {upload.Status}.");
    }

    private static APIRequestContextOptions Json(object body) => new() { DataObject = body };

    private static async Task<JsonElement> JsonAsync(IAPIResponse response)
    {
        response.Ok.ShouldBeTrue($"{response.Url} answered {response.Status}.");
        return JsonDocument.Parse(await response.TextAsync()).RootElement;
    }
}
