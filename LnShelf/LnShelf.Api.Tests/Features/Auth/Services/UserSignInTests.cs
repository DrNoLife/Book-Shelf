using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>
/// What a provider's identity becomes: an existing user, the adopted initial owner, a new
/// user, or nothing.
/// </summary>
public sealed class UserSignInTests
{
    private const string Issuer = "https://accounts.google.com";
    private const string OwnerEmail = "owner@example.com";

    [Fact]
    public async Task SignInAsync_ANewIdentity_CreatesAUserAndPutsItInTheSession()
    {
        await using var dbContext = CreateDbContext();

        var principal = await SignIn(dbContext).SignInAsync(Identity("sub-1", "new@example.com"), CancellationToken.None);

        var user = await dbContext.Users.SingleAsync();
        user.Email.ShouldBe("new@example.com");
        principal.ShouldNotBeNull().FindFirst(LnShelfClaims.UserId)!.Value.ShouldBe(user.Id.ToString());
    }

    [Fact]
    public async Task SignInAsync_TheSameIdentityTwice_IsOneUser()
    {
        await using var dbContext = CreateDbContext();
        var signIn = SignIn(dbContext);

        var first = await signIn.SignInAsync(Identity("sub-1", "new@example.com"), CancellationToken.None);
        var second = await signIn.SignInAsync(Identity("sub-1", "new@example.com"), CancellationToken.None);

        (await dbContext.Users.CountAsync()).ShouldBe(1);
        second!.FindFirst(LnShelfClaims.UserId)!.Value.ShouldBe(first!.FindFirst(LnShelfClaims.UserId)!.Value);
    }

    // The library from before users belongs to the placeholder the migration made, and the
    // configured address takes it over in place.
    [Theory]
    [InlineData("owner@example.com")]
    [InlineData("Owner@Example.COM")]
    public async Task SignInAsync_TheInitialOwner_AdoptsThePlaceholder(string email)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(Placeholder());
        await dbContext.SaveChangesAsync();

        var principal = await SignIn(dbContext).SignInAsync(Identity("sub-owner", email), CancellationToken.None);

        principal.ShouldNotBeNull().FindFirst(LnShelfClaims.UserId)!.Value
            .ShouldBe(User.InitialOwnerPlaceholderId.ToString());
        var adopted = await dbContext.Users.SingleAsync();
        adopted.Issuer.ShouldBe(Issuer);
        adopted.Subject.ShouldBe("sub-owner");
    }

    [Fact]
    public async Task SignInAsync_SomeoneElse_DoesNotGetThePlaceholder()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(Placeholder());
        await dbContext.SaveChangesAsync();

        var principal = await SignIn(dbContext).SignInAsync(Identity("sub-2", "stranger@example.com"), CancellationToken.None);

        principal.ShouldNotBeNull().FindFirst(LnShelfClaims.UserId)!.Value
            .ShouldNotBe(User.InitialOwnerPlaceholderId.ToString());
        (await dbContext.Users.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task SignInAsync_AnUnverifiedAddress_IsRefused()
    {
        await using var dbContext = CreateDbContext();

        var principal = await SignIn(dbContext).SignInAsync(
            Identity("sub-1", OwnerEmail, verified: false), CancellationToken.None);

        principal.ShouldBeNull();
        (await dbContext.Users.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SignInAsync_AnAddressOutsideTheAllowList_IsRefused()
    {
        await using var dbContext = CreateDbContext();

        var principal = await SignIn(dbContext, allowedEmails: ["friend@example.com"])
            .SignInAsync(Identity("sub-1", "stranger@example.com"), CancellationToken.None);

        principal.ShouldBeNull();
    }

    // The picture is handed on to be copied; the name that comes with the same scope is not
    // read, and the user row has nowhere to put one.
    [Theory]
    [InlineData("https://lh3.googleusercontent.com/a/picture")]
    [InlineData(null)]
    public async Task SignInAsync_ThePictureClaim_IsHandedToTheAvatarCopy(string? pictureUrl)
    {
        await using var dbContext = CreateDbContext();
        var avatarCommand = Substitute.For<IAvatarCommandService>();

        var identity = Identity("sub-1", "new@example.com");
        ((ClaimsIdentity)identity.Identity!).AddClaim(new Claim("name", "Thor Christiansen"));
        if (pictureUrl is not null)
        {
            ((ClaimsIdentity)identity.Identity!).AddClaim(new Claim("picture", pictureUrl));
        }

        var principal = await SignIn(dbContext, avatarCommand: avatarCommand).SignInAsync(identity, CancellationToken.None);

        principal.ShouldNotBeNull();
        var user = await dbContext.Users.SingleAsync();
        await avatarCommand.Received(1).RefreshProviderCopyAsync(user, pictureUrl, Arg.Any<CancellationToken>());
    }

    private static ClaimsPrincipal Identity(string subject, string email, bool verified = true) =>
        new(new ClaimsIdentity(
            [
                new Claim("iss", Issuer),
                new Claim("sub", subject),
                new Claim("email", email),
                new Claim("email_verified", verified ? "true" : "false"),
            ],
            "test"));

    private static User Placeholder() => new()
    {
        Id = User.InitialOwnerPlaceholderId,
        Issuer = User.LocalIssuer,
        Subject = "initial-owner",
        Email = string.Empty,
    };

    private static UserSignIn SignIn(
        LnShelfDbContext dbContext,
        string[]? allowedEmails = null,
        IAvatarCommandService? avatarCommand = null) =>
        new(
            dbContext,
            Options.Create(new AuthOptions { InitialOwnerEmail = OwnerEmail, AllowedEmails = allowedEmails ?? [] }),
            avatarCommand ?? Substitute.For<IAvatarCommandService>(),
            TimeProvider.System,
            NullLogger<UserSignIn>.Instance);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"user-sign-in-{Guid.CreateVersion7()}")
            .Options, TestUsers.As(null));
}
