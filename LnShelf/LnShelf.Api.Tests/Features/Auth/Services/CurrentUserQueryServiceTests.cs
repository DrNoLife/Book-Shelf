using LnShelf.Api.Features.Auth.Mappers;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>
/// "Who am I" answers for the session's own user and for nobody else.
/// </summary>
public sealed class CurrentUserQueryServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsTheSignedInUsersOwnRowAndNeverAnotherUsers()
    {
        var database = $"current-user-{Guid.CreateVersion7()}";
        await using var dbContext = Context(database, TestUsers.Owner);
        dbContext.Users.Add(User(TestUsers.Owner, "thor.christiansen@example.com"));
        dbContext.Users.Add(User(TestUsers.SomeoneElse, "someone@example.com"));
        await dbContext.SaveChangesAsync();

        var user = await new CurrentUserQueryService(dbContext, TestUsers.Current).GetAsync(CancellationToken.None);

        user.ShouldNotBeNull();
        user.Email.ShouldBe("thor.christiansen@example.com");
        user.DisplayName.ShouldBe("Thor Christiansen");
    }

    // The upload wins over the provider's copy, and the version is the picture's own id, so
    // it changes whenever the picture does.
    [Theory]
    [InlineData(null, null, null, false)]
    [InlineData("avatar/u/0198a1b2c3d47e5f8a9b0c1d2e3f4a5b.webp", null, "0198a1b2c3d47e5f8a9b0c1d2e3f4a5b", false)]
    [InlineData("avatar/u/0198a1b2c3d47e5f8a9b0c1d2e3f4a5b.webp", "avatar/u/1198a1b2c3d47e5f8a9b0c1d2e3f4a5b.webp", "1198a1b2c3d47e5f8a9b0c1d2e3f4a5b", true)]
    public async Task GetAsync_TheAvatar_IsTheUploadElseTheProviderCopy(
        string? providerKey, string? overrideKey, string? expectedVersion, bool expectedUploaded)
    {
        await using var dbContext = Context($"current-user-{Guid.CreateVersion7()}", TestUsers.Owner);
        var user = User(TestUsers.Owner, "owner@example.com");
        user.ProviderAvatarObjectKey = providerKey;
        user.AvatarOverrideObjectKey = overrideKey;
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await new CurrentUserQueryService(dbContext, TestUsers.Current).GetAsync(CancellationToken.None);

        response.ShouldNotBeNull().AvatarVersion.ShouldBe(expectedVersion);
        response.HasUploadedAvatar.ShouldBe(expectedUploaded);
    }

    [Fact]
    public async Task GetAsync_TheUsersRowIsGone_ReturnsNull()
    {
        await using var dbContext = Context($"current-user-{Guid.CreateVersion7()}", TestUsers.Owner);

        (await new CurrentUserQueryService(dbContext, TestUsers.Current).GetAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Theory]
    [InlineData("thor.christiansen@example.com", "Thor Christiansen")]
    [InlineData("lazy_t@example.com", "Lazy T")]
    [InlineData("neko@example.com", "Neko")]
    [InlineData("...@example.com", "...@example.com")]
    public void DisplayNameMapper_FromEmail_TurnsTheLocalPartIntoWords(string email, string expected) =>
        DisplayNameMapper.FromEmail(email).ShouldBe(expected);

    private static User User(Guid id, string email) => new()
    {
        Id = id,
        Issuer = "https://accounts.google.com",
        Subject = $"sub-{id}",
        Email = email,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));
}
