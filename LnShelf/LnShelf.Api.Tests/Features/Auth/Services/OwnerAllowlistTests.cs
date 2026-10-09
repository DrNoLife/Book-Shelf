using System.Security.Claims;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>Who gets a session: the configured address, verified, and nobody else.</summary>
public sealed class OwnerAllowlistTests
{
    private const string Owner = "owner@example.com";

    [Theory]
    [InlineData("owner@example.com", "true", true)]
    [InlineData("Owner@Example.COM", "true", true)]
    [InlineData("owner@example.com", "false", false)]
    [InlineData("owner@example.com", null, false)]
    [InlineData("someone.else@example.com", "true", false)]
    [InlineData(null, "true", false)]
    public void Admits_AnIdentity_OnlyTheVerifiedOwner(string? email, string? emailVerified, bool expected)
    {
        var claims = new List<Claim>();
        if (email is not null) claims.Add(new Claim("email", email));
        if (emailVerified is not null) claims.Add(new Claim("email_verified", emailVerified));

        OwnerAllowlist.Admits(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), Owner).ShouldBe(expected);
    }

    [Fact]
    public void Admits_NoAllowedAddressConfigured_AdmitsNobody()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("email", Owner), new Claim("email_verified", "true")], "test"));

        OwnerAllowlist.Admits(principal, string.Empty).ShouldBeFalse();
    }
}
