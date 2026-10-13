namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What one refresh may still spend, per per-volume source: the store catalogue's requests
/// and the by-ISBN lookups, drawn down separately.
/// </summary>
// Two budgets rather than one. They used to be one (OpenLibraryOptions' numbers), which let a
// series with no remembered store title spend nine title attempts against an on-demand
// allowance of eight before a single ISBN was looked up. The two APIs have unrelated limits,
// so each gets its own allowance (CLAUDE.md, be a polite API citizen).
public sealed class EnrichmentBudget(LookupBudget catalogue, LookupBudget byIsbn)
{
    public EnrichmentBudget(int catalogueRequests, int isbnLookups)
        : this(new LookupBudget(catalogueRequests), new LookupBudget(isbnLookups))
    {
    }

    /// <summary>Store catalogue requests (Apple Books), one per title attempted.</summary>
    public LookupBudget Catalogue { get; } = catalogue ?? throw new ArgumentNullException(nameof(catalogue));

    /// <summary>By-ISBN requests (Open Library), direct lookups and fallback searches.</summary>
    public LookupBudget ByIsbn { get; } = byIsbn ?? throw new ArgumentNullException(nameof(byIsbn));
}
