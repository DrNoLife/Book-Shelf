namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Keeps each cover as a file under one folder, the key's <c>/</c>-separated segments as
/// subfolders.
/// </summary>
// A folder rather than an object store: one server, covers of 50-150 KB, and a backup that is a
// tar of one directory (feature-spec §3). MinIO filled this role until its community edition
// stopped shipping.
public sealed class FileSystemCoverStore(CoverStoreRoot root) : ICoverStore
{
    // Linux is case-sensitive and the server runs Linux; Windows is only a development box,
    // where two keys differing in case would be one file anyway.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly char[] ForbiddenSegmentCharacters =
        [.. Path.GetInvalidFileNameChars().Union(['\\', ':'])];

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(File.Exists(PathFor(objectKey)));
    }

    public async Task SaveAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var path = PathFor(objectKey);
        var detected = CoverImageSignature.ContentTypeOf(content.Span);

        // The folder keeps no content type, so reads derive it from the bytes. Refusing a
        // mismatch here keeps that honest: what a caller saves is what a reader is told.
        if (detected is CoverImageSignature.Unknown || !string.Equals(detected, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The content is {detected}, not the {contentType} it was saved as.", nameof(contentType));
        }

        Directory.CreateDirectory(root.StagingPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var staged = Path.Combine(root.StagingPath, Guid.NewGuid().ToString("N"));

        try
        {
            await using (var stream = new FileStream(
                staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(content, cancellationToken);

                // Through to the disk before the rename: otherwise a power cut can leave a
                // zero-length file under the final name, which reads as a broken cover forever.
                stream.Flush(flushToDisk: true);
            }

            // Atomic within one filesystem: a reader sees the old cover or the new one, never
            // half of either. Two writers racing on one key both leave a complete file; the
            // last rename wins.
            File.Move(staged, path, overwrite: true);
        }
        finally
        {
            // Only still there if something failed or was cancelled before the rename.
            File.Delete(staged);
        }
    }

    public async Task<CoverContent?> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var path = PathFor(objectKey);

        // Read and handle "not there" rather than check first: a delete between the check and
        // the read would otherwise surface as a server error instead of a plain miss.
        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);

            return new CoverContent(content, CoverImageSignature.ContentTypeOf(content));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // File.Delete is already silent about a missing file; only a missing folder throws.
        try
        {
            File.Delete(PathFor(objectKey));
        }
        catch (DirectoryNotFoundException)
        {
        }

        return Task.CompletedTask;
    }

    /// <remarks>
    /// The prefix is a plain string prefix, as in S3: <c>override/vol</c> matches
    /// <c>override/volume/…</c>. A key cannot be both a file and a folder (<c>a/b</c> and
    /// <c>a/b/c</c>), which none of today's key shapes ever asks for.
    /// </remarks>
    public IAsyncEnumerable<string> ListKeysAsync(string prefix, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return EnumerateKeys(prefix, cancellationToken).ToAsyncEnumerable();
    }

    private IEnumerable<string> EnumerateKeys(string prefix, CancellationToken cancellationToken)
    {
        // Walk only the folder the prefix's complete segments name, then match the rest as a
        // string, which is what makes a prefix that stops mid-segment work.
        var folderPart = prefix[..(prefix.LastIndexOf('/') + 1)];
        var folder = folderPart.Length is 0 ? root.Path : PathFor(folderPart.TrimEnd('/'));

        if (!Directory.Exists(folder))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        });

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = Path.GetRelativePath(root.Path, file).Replace(Path.DirectorySeparatorChar, '/');

            // Dot segments are never keys (PathFor refuses them): this is the staging folder.
            if (key.Split('/').Any(segment => segment.StartsWith('.')))
            {
                continue;
            }

            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return key;
            }
        }
    }

    /// <summary>
    /// The one place a key becomes a path, and the guard that it stays under the root.
    /// </summary>
    // On top of CoverObjectKey.IsWellFormed, which the controllers check: the store is the last
    // thing between a key and the disk, so it refuses anything that could leave the folder or
    // that two different keys could both map to.
    private string PathFor(string objectKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(objectKey);

        foreach (var segment in objectKey.Split('/'))
        {
            if (segment.Length is 0 || segment.StartsWith('.') || segment.IndexOfAny(ForbiddenSegmentCharacters) >= 0)
            {
                throw new ArgumentException($"'{objectKey}' is not a valid cover key.", nameof(objectKey));
            }
        }

        var path = Path.GetFullPath(Path.Combine(root.Path, objectKey));

        // The separator is part of the check, so a sibling folder such as "covers2" never
        // passes for "covers".
        if (!path.StartsWith(root.Path + Path.DirectorySeparatorChar, PathComparison))
        {
            throw new ArgumentException($"'{objectKey}' points outside the cover folder.", nameof(objectKey));
        }

        return path;
    }
}
