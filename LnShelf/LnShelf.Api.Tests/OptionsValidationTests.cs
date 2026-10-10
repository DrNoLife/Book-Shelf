using LnShelf.Api.Features.Covers;
using LnShelf.Api.Features.Import;
using LnShelf.Api.Features.Metadata;
using LnShelf.Api.Features.Sharing;
using LnShelf.Api.Features.Shelf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests;

/// <summary>
/// Configuration that cannot work is refused at boot, by name.
/// </summary>
// Written for one concrete failure path: Sync__Hour is set in the production compose file,
// and a value outside 0-23 would bind cleanly, then throw ArgumentOutOfRangeException from
// inside the nightly loop — which .NET answers by stopping the host, hours after the
// deployment, with a trace that names neither the setting nor the file it came from.
//
// IStartupValidator is what the host calls during StartAsync, so calling it here is the same
// check the running app makes, without hosting anything.
public sealed class OptionsValidationTests
{
    [Fact]
    public void ShippedDefaults_PassValidation()
    {
        // No configuration at all: every options type falls back to the value written into
        // its own class. If a bound in this codebase ever excludes its own default, this is
        // the test that says so.
        var act = () => Validate();

        act.ShouldNotThrow();
    }

    [Fact]
    public void ShippedAppsettings_PassStartupValidation()
    {
        // ShippedDefaults_PassValidation above only proves each class's own C# default is
        // valid — it never reads a file. OpenLibrary and AppleBooks both shipped a
        // CacheDuration of "24:00:00" in the real appsettings.json: TimeSpan parses that as
        // 24 days (the d:hh:mm:ss shape), comfortably outside the 7-day ceiling, while the
        // C# default stayed at one day and so never saw the bad value. Only reading the file
        // the host actually reads at boot catches that.
        var act = () => ValidateAppsettingsFile(includeDevelopmentOverlay: false);

        act.ShouldNotThrow();
    }

    [Fact]
    public void ShippedAppsettingsWithDevelopmentOverlay_PassStartupValidation()
    {
        // appsettings.Development.json does not currently override any validated key, but the
        // host layers it over the base file at boot the same way (Program.cs relies on the
        // standard ASP.NET Core convention), so the merged result — not the base file alone
        // — is what should be asserted. A future override landing on a validated key without
        // passing this is the point of keeping it separate from the test above.
        var act = () => ValidateAppsettingsFile(includeDevelopmentOverlay: true);

        act.ShouldNotThrow();
    }

    [Fact]
    public void SyncHourOutsideTheDay_FailsAtStartup_NamingTheField()
    {
        var exception = Should.Throw<OptionsValidationException>(
            () => Validate(("Sync:Hour", "24")));

        exception.Message.ShouldContain("Hour");
        exception.Message.ShouldContain("23");
    }

    [Theory]
    // One per options type, so a slice that loses its ValidateDataAnnotations call is caught
    // rather than only the slice somebody happened to think of.
    [InlineData("Sync:Hour", "-1", "Hour")]
    [InlineData("AniList:RequestsPerMinute", "0", "RequestsPerMinute")]
    [InlineData("MangaUpdates:AutoLinkMinimumScore", "101", "AutoLinkMinimumScore")]
    [InlineData("AppleBooks:SearchLimit", "0", "SearchLimit")]
    [InlineData("OpenLibrary:MaximumLookupsPerRun", "0", "MaximumLookupsPerRun")]
    [InlineData("OpenLibrary:RequestTimeout", "01:00:00", "RequestTimeout")]
    [InlineData("Covers:FileSystem:RootPath", "", "RootPath")]
    [InlineData("Covers:CoverQuality", "0", "CoverQuality")]

    // One byte over the cover endpoint's own request limit: a value above it could never
    // take effect, so it is refused rather than silently ignored.
    [InlineData("Covers:MaximumUploadBytes", "8388609", "MaximumUploadBytes")]
    [InlineData("Sharing:RequestsPerMinute", "0", "RequestsPerMinute")]
    [InlineData("Sharing:CoverRequestsPerMinute", "0", "CoverRequestsPerMinute")]
    public void AnOutOfRangeSetting_FailsAtStartup_NamingTheField(
        string key, string value, string expectedFieldName)
    {
        var exception = Should.Throw<OptionsValidationException>(
            () => Validate((key, value)));

        exception.Message.ShouldContain(expectedFieldName);
    }

    /// <summary>
    /// Runs the startup validation the host would run, over the real slice registrations.
    /// </summary>
    private static void Validate(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(
                setting => setting.Key, setting => (string?)setting.Value))
            .Build();

        ValidateConfiguration(configuration);
    }

    /// <summary>
    /// The same validation, over the configuration files the host actually reads at boot
    /// rather than in-memory settings built for one scenario.
    /// </summary>
    // Linked from the Api project (LnShelf.Api.Tests.csproj), not copied, so this can never
    // check a stale duplicate of what ships.
    private static void ValidateAppsettingsFile(bool includeDevelopmentOverlay)
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "appsettings.json"), optional: false);

        if (includeDevelopmentOverlay)
        {
            configurationBuilder.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "appsettings.Development.json"),
                optional: false);
        }

        ValidateConfiguration(configurationBuilder.Build());
    }

    /// <summary>
    /// Wires the real slice registrations over the given configuration, then runs the same
    /// check the host runs during <c>StartAsync</c>.
    /// </summary>
    // Every slice that owns validated options is registered here — Shelf, Metadata (which
    // covers AniList, MangaUpdates, Open Library, Apple Books and Sync internally), Covers,
    // Import and Sharing — and nothing is resolved beyond the validator: whether the whole graph can
    // be constructed is CompositionRootTests' question, not this one's.
    private static void ValidateConfiguration(IConfiguration configuration)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddShelf(configuration);
        services.AddMetadata(configuration);
        services.AddCovers(configuration);
        services.AddImport(configuration);
        services.AddSharing(configuration);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
    }
}
