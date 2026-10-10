namespace LnShelf.Api.Features.Auth.Services;

/// <inheritdoc />
// Scoped: one per request or per background unit of work. Reads the session's user claim,
// which UserSignIn put there at sign-in, unless something in the scope has called ActAs.
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private Guid? _actingAs;

    public Guid? IdOrNull => _actingAs ?? FromClaim();

    public Guid Id => IdOrNull
        ?? throw new InvalidOperationException("This work has no current user. Background work must call ActAs first.");

    public void ActAs(Guid userId) => _actingAs = userId;

    private Guid? FromClaim()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst(LnShelfClaims.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
