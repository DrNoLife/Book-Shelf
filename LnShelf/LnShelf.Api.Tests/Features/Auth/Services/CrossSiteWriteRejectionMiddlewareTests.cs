namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>A write the browser says came from another site is refused; anything else passes.</summary>
public sealed class CrossSiteWriteRejectionMiddlewareTests
{
    [Theory]
    [InlineData("POST", "cross-site", true)]
    [InlineData("PUT", "cross-site", true)]
    [InlineData("DELETE", "Cross-Site", true)]
    [InlineData("POST", "same-origin", false)]
    [InlineData("POST", "same-site", false)]
    [InlineData("POST", null, false)]
    [InlineData("GET", "cross-site", false)]
    [InlineData("HEAD", "cross-site", false)]
    public void ShouldReject_AMethodAndOrigin_RejectsOnlyCrossSiteWrites(string method, string? secFetchSite, bool expected) =>
        CrossSiteWriteRejectionMiddleware.ShouldReject(method, secFetchSite).ShouldBe(expected);
}
