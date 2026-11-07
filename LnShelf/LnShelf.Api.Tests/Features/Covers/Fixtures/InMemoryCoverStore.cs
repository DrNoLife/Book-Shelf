namespace LnShelf.Api.Tests.Features.Covers.Fixtures;

/// <summary>
/// A cover store that keeps objects in a dictionary, so a test can put bytes in and read back
/// exactly what a service wrote, where a substitute would only record the calls.
/// </summary>
internal sealed class InMemoryCoverStore : ICoverStore
{
    private readonly Dictionary<string, CoverContent> _objects = [];
    private readonly Dictionary<string, DateTimeOffset> _lastModified = [];

    public IReadOnlyCollection<string> Keys => _objects.Keys;

    public void Put(string objectKey, byte[] content, string contentType = "image/webp")
    {
        _objects[objectKey] = new CoverContent(content, contentType);
        _lastModified[objectKey] = DateTimeOffset.UtcNow;
    }

    /// <summary>Backdates an object, for a test about how old a file has to be.</summary>
    public void SetLastModified(string objectKey, DateTimeOffset lastModified) =>
        _lastModified[objectKey] = lastModified;

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.ContainsKey(objectKey));

    public Task SaveAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken)
    {
        Put(objectKey, content.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<CoverContent?> ReadAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.GetValueOrDefault(objectKey));

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        _objects.Remove(objectKey);
        _lastModified.Remove(objectKey);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ListKeysAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var key in _objects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            yield return key;
        }

        await Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLastModifiedAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult<DateTimeOffset?>(
            _lastModified.TryGetValue(objectKey, out var lastModified) ? lastModified : null);
}
