namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// Names an image format from its first bytes.
/// </summary>
// A folder has no object metadata to keep a content type in, and the key's extension is not
// trustworthy (a fetched cover keyed ".jpg" holds webp once normalised), so the bytes are the
// one honest answer. A sidecar file would be a second write that can disagree with the first.
public static class CoverImageSignature
{
    public const string Unknown = "application/octet-stream";

    public static string ContentTypeOf(ReadOnlySpan<byte> content) => content switch
    {
        // RIFF....WEBP
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        // GIF87a / GIF89a
        [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..] => "image/gif",
        _ => Unknown,
    };
}
