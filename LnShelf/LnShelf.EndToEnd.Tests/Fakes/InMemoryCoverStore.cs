using System.Collections.Concurrent;

namespace LnShelf.EndToEnd.Tests.Fakes;

/// <summary>
/// <c>ICoverStore</c> without MinIO, whose community image can no longer be pulled
/// (docs/backlog.md). One instance lives for the whole run and is cleared between tests.
/// </summary>
public sealed class InMemoryCoverStore : ICoverStore
{
    private readonly ConcurrentDictionary<string, CoverContent> _covers = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> ObjectKeys => [.. _covers.Keys];

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_covers.ContainsKey(objectKey));

    public Task SaveAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken)
    {
        _covers[objectKey] = new CoverContent(content.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<CoverContent?> ReadAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_covers.GetValueOrDefault(objectKey));

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        _covers.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ListKeysAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var key in _covers.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            yield return key;
        }

        await Task.CompletedTask;
    }

    public void Clear() => _covers.Clear();
}
