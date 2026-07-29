namespace LnShelf.Api.Features.Covers.Models;

/// <summary>One stored cover, ready to write to a response.</summary>
public sealed record CoverContent(byte[] Content, string ContentType);
