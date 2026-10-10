namespace LnShelf.Api.Features.Auth.Abstractions;

/// <summary>
/// Who the work in this scope is for. A request is the signed-in user; background work and
/// console commands say who they are acting for with <see cref="ActAs"/>.
/// </summary>
// The owner filters on LnShelfDbContext read this, so a scope with no user sees no rows and
// cannot save an owned row: forgetting to set it fails closed, never open.
public interface ICurrentUser
{
    /// <summary>The user, or null when the scope belongs to nobody.</summary>
    Guid? IdOrNull { get; }

    /// <summary>The user. Throws when the scope belongs to nobody.</summary>
    Guid Id { get; }

    /// <summary>Makes the rest of this scope act for <paramref name="userId"/>.</summary>
    void ActAs(Guid userId);
}
