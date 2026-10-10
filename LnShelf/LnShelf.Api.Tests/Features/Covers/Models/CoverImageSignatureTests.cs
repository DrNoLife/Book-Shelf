namespace LnShelf.Api.Tests.Features.Covers.Models;

/// <summary>
/// The folder store's content types come from here, so a wrong answer is a cover the browser
/// refuses to draw.
/// </summary>
public sealed class CoverImageSignatureTests
{
    public static TheoryData<byte[], string> Signatures => new()
    {
        { [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56], "image/webp" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "image/png" },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00], "image/jpeg" },
        { [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01], "image/gif" },
        { [0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x01], "image/gif" },

        // RIFF but not WEBP (a WAV file), and things too short to be anything.
        { [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45], CoverImageSignature.Unknown },
        { [0xFF, 0xD8], CoverImageSignature.Unknown },
        { [], CoverImageSignature.Unknown },
    };

    [Theory]
    [MemberData(nameof(Signatures))]
    public void ContentTypeOf_LeadingBytes_NamesTheFormat(byte[] content, string expected) =>
        CoverImageSignature.ContentTypeOf(content).ShouldBe(expected);
}
