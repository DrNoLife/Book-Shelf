using Microsoft.Extensions.Hosting;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>
/// The login refuses to start half-configured, and the development bypass refuses to start
/// anywhere but Development.
/// </summary>
public sealed class AuthOptionsValidatorTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("EndToEnd")]
    [InlineData("Staging")]
    public void Validate_BypassOutsideDevelopment_Fails(string environmentName)
    {
        var result = Validator(environmentName).Validate(null, new AuthOptions { DevelopmentBypass = true });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("only allowed in Development");
    }

    [Fact]
    public void Validate_BypassInDevelopment_NeedsNothingElse() =>
        Validator("Development").Validate(null, new AuthOptions { DevelopmentBypass = true }).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("", "secret", "Auth:ClientId")]
    [InlineData("client", "", "Auth:ClientSecret")]
    public void Validate_AMissingSetting_FailsNamingIt(string clientId, string clientSecret, string expected)
    {
        var result = Validator("Production").Validate(null, new AuthOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(expected);
    }

    [Fact]
    public void Validate_EverythingSet_Succeeds() =>
        Validator("Production").Validate(null, new AuthOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
        }).Succeeded.ShouldBeTrue();

    private static AuthOptionsValidator Validator(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new AuthOptionsValidator(environment);
    }
}
