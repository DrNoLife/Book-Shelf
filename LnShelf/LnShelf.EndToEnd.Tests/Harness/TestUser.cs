using System.Security.Claims;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// The two people the journeys sign in as. A owns the seeded shelf; B starts empty, which is
/// what the isolation journeys need.
/// </summary>
public sealed record TestUser(string Key, string Email)
{
    public const string Issuer = "https://end-to-end.test";

    public static TestUser A { get; } = new("a", "a@example.com");

    public static TestUser B { get; } = new("b", "b@example.com");

    public static TestUser? FromKey(string? key) => key switch
    {
        "a" => A,
        "b" => B,
        _ => null,
    };

    /// <summary>What the provider would have vouched for, as UserSignIn reads it.</summary>
    public ClaimsPrincipal ProviderIdentity() =>
        new(new ClaimsIdentity(
            [
                new Claim("iss", Issuer),
                new Claim("sub", Key),
                new Claim("email", Email),
                new Claim("email_verified", "true"),
            ],
            "end-to-end"));
}
