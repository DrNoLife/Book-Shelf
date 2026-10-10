using System.Runtime.CompilerServices;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
public sealed class MinioCoverStore(
    IMinioClient minioClient,
    IOptions<CoverStoreOptions> options,
    ILogger<MinioCoverStore> logger) : ICoverStore, IDisposable
{
    // The bucket is created on first use rather than at startup: MinIO being down
    // must not stop the API from booting, because every other feature works
    // without covers.
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketReady;

    private string Bucket => options.Value.Bucket;

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        try
        {
            await minioClient.StatObjectAsync(
                new StatObjectArgs().WithBucket(Bucket).WithObject(objectKey),
                cancellationToken);

            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }

    public async Task SaveAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        using var source = new MemoryStream(content.ToArray(), writable: false);

        await minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(Bucket)
                .WithObject(objectKey)
                .WithStreamData(source)
                .WithObjectSize(source.Length)
                .WithContentType(contentType),
            cancellationToken);
    }

    public async Task<CoverContent?> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        using var buffer = new MemoryStream();

        try
        {
            var stat = await minioClient.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(Bucket)
                    .WithObject(objectKey)
                    .WithCallbackStream((stream, token) => stream.CopyToAsync(buffer, token)),
                cancellationToken);

            return new CoverContent(buffer.ToArray(), stat.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    // S3 semantics already make this idempotent: removing a missing object succeeds, so
    // there is no not-found case to translate.
    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        await minioClient.RemoveObjectAsync(
            new RemoveObjectArgs().WithBucket(Bucket).WithObject(objectKey),
            cancellationToken);
    }

    public async IAsyncEnumerable<string> ListKeysAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        var arguments = new ListObjectsArgs().WithBucket(Bucket).WithPrefix(prefix).WithRecursive(true);

        await foreach (var item in minioClient.ListObjectsEnumAsync(arguments, cancellationToken))
        {
            yield return item.Key;
        }
    }

    public void Dispose() => _bucketGate.Dispose();

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
            {
                return;
            }

            var exists = await minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(Bucket), cancellationToken);

            if (!exists)
            {
                await minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(Bucket), cancellationToken);

                logger.LogInformation("Created the cover bucket {Bucket}", Bucket);
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }
}
