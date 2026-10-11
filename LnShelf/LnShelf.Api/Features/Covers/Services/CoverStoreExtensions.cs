namespace LnShelf.Api.Features.Covers.Services;

public static class CoverStoreExtensions
{
    extension(ICoverStore coverStore)
    {
        /// <summary>
        /// Removes an object nothing points at any more, logging instead of throwing when the
        /// store refuses.
        /// </summary>
        // For deletes that follow a saved change: the change already happened, and failing the
        // request over a leftover file would report a write that succeeded as one that did
        // not. A leftover is unreferenced, and cover-orphan-sweep removes it.
        public async Task DeleteUnreferencedAsync(string? objectKey, ILogger logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(logger);

            if (objectKey is null)
            {
                return;
            }

            try
            {
                await coverStore.DeleteAsync(objectKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not delete {ObjectKey}; it is orphaned but unreferenced", objectKey);
            }
        }
    }
}
