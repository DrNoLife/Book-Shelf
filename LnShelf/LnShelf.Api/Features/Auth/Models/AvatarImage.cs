namespace LnShelf.Api.Features.Auth.Models;

/// <summary>The avatar a user sees, ready to write to a response.</summary>
/// <param name="Version">What the client puts in the avatar URL (CoverObjectKey.AvatarVersionOf).</param>
public sealed record AvatarImage(byte[] Content, string ContentType, string Version);
