namespace LnShelf.Api.Features.Covers.Abstractions;

/// <summary>
/// The named seam for cover storage (feature-spec §3). MinIO over the S3 API is
/// the first implementation; a bind-mounted folder or another S3 target is a new
/// implementation, not an edit.
/// </summary>
// Bytes rather than streams, deliberately: a cover is 50-150 KB, one user's whole
// shelf is a few thousand of them, and a byte array removes the disposal contract
// a Stream return would put on every caller. If this ever stores something large,
// that is the moment to add a streaming overload.
public interface ICoverStore
{
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);

    Task SaveAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken);

    /// <returns>Null when the store holds nothing under that key.</returns>
    Task<CoverContent?> ReadAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the object under a key. Deleting a key that holds nothing is not an error.
    /// </summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Every key under <paramref name="prefix"/>, however deep.
    /// </summary>
    // Streamed, because a store can hold far more keys than anything wants in a list at
    // once. Exists for the orphan sweep (Features/Covers/Commands); nothing on a request
    // path lists the bucket.
    IAsyncEnumerable<string> ListKeysAsync(string prefix, CancellationToken cancellationToken);
}
