namespace LnShelf.Api.Features.Auth.Abstractions;

/// <summary>Reads a copy of everything stored about the signed-in user.</summary>
public interface IAccountExportQueryService
{
    /// <summary>The signed-in user's data, or null when their row no longer exists.</summary>
    Task<AccountExportResponse?> ExportAsync(CancellationToken cancellationToken);
}
