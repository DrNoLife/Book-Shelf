using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// A synced cover key whose file the store no longer holds is forgotten, and nothing else is.
/// </summary>
// The regression behind it (2026-10-13): 153 volumes kept keys to files that were gone, so
// every rule downstream counted them as covered, the sync matched each of them without
// fetching a thing, and the shelf showed the series art on every tile.
public sealed class DanglingVolumeCoverHealerTests
{
    private const string PresentKey = "volume/applebooks/present.jpg";
    private const string MissingKey = "volume/applebooks/missing.jpg";

    [Theory]
    [InlineData(MissingKey, null)]
    [InlineData(PresentKey, PresentKey)]
    [InlineData(null, null)]
    public async Task HealAsync_ACoverKey_IsKeptOnlyWhenTheStoreHoldsItsFile(string? key, string? expected)
    {
        var volume = Volume(1);
        volume.CoverObjectKey = key;
        var series = Series("86―EIGHTY-SIX", [volume]);

        await Healer().HealAsync(series, CancellationToken.None);

        volume.CoverObjectKey.ShouldBe(expected);
    }

    [Fact]
    public async Task HealAsync_AVolumeWithNoKey_DoesNotAskTheStore()
    {
        var store = Store();
        var series = Series("86―EIGHTY-SIX", [Volume(1)]);

        await new DanglingVolumeCoverHealer(store, NullLogger<DanglingVolumeCoverHealer>.Instance)
            .HealAsync(series, CancellationToken.None);

        await store.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealAsync_AVolumeWithADanglingKeyAndAnOverride_LeavesTheOverrideAlone()
    {
        var volume = Volume(1);
        volume.CoverObjectKey = MissingKey;
        volume.UserState = new VolumeUserState { CoverOverrideObjectKey = "override/also-missing.webp" };
        var series = Series("86―EIGHTY-SIX", [volume]);

        await Healer().HealAsync(series, CancellationToken.None);

        // User data: sync never writes it, not even to tidy a key that points nowhere.
        volume.CoverObjectKey.ShouldBeNull();
        volume.UserState.CoverOverrideObjectKey.ShouldBe("override/also-missing.webp");
    }

    private static DanglingVolumeCoverHealer Healer() =>
        new(Store(), NullLogger<DanglingVolumeCoverHealer>.Instance);

    private static ICoverStore Store()
    {
        var store = Substitute.For<ICoverStore>();
        store.ExistsAsync(PresentKey, Arg.Any<CancellationToken>()).Returns(true);
        store.ExistsAsync(MissingKey, Arg.Any<CancellationToken>()).Returns(false);
        return store;
    }
}
