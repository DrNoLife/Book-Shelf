namespace LnShelf.Api.Data;

/// <summary>
/// A root entity that belongs to one user. Its children belong to the same user through it.
/// </summary>
// LnShelfDbContext stamps OwnerId on every added IOwned row from ICurrentUser and filters
// every query by it, so services neither set nor check the owner themselves.
public interface IOwned
{
    Guid OwnerId { get; set; }
}
