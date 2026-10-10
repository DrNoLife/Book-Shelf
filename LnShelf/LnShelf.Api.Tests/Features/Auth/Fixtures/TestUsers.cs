namespace LnShelf.Api.Tests.Features.Auth.Fixtures;

/// <summary>
/// The user unit tests act as. Every test context is this user's, so the owner filters and
/// owner stamping run in every test exactly as they do in a request.
/// </summary>
internal static class TestUsers
{
    public static readonly Guid Owner = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SomeoneElse = new("22222222-2222-2222-2222-222222222222");

    public static ICurrentUser Current => new FixedCurrentUser(Owner);

    public static ICurrentUser As(Guid? userId) => new FixedCurrentUser(userId);

    /// <summary>The default limits, counted against the context's own user.</summary>
    public static UserLimits Limits(LnShelfDbContext dbContext) =>
        new(dbContext, Options.Create(new UserLimitOptions()));

    private sealed class FixedCurrentUser(Guid? userId) : ICurrentUser
    {
        private Guid? _userId = userId;

        public Guid? IdOrNull => _userId;

        public Guid Id => _userId ?? throw new InvalidOperationException("No current user.");

        public void ActAs(Guid userId) => _userId = userId;
    }
}
