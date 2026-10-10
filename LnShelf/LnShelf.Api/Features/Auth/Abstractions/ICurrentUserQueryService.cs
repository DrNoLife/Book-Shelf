namespace LnShelf.Api.Features.Auth.Abstractions;

/// <summary>Reads about the signed-in user.</summary>
public interface ICurrentUserQueryService
{
    /// <summary>The signed-in user, or null when their row no longer exists.</summary>
    Task<CurrentUserResponse?> GetAsync(CancellationToken cancellationToken);
}
