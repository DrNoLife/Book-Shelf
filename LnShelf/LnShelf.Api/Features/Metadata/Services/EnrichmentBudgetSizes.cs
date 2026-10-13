namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Sizes the per-volume allowances for a run, a night and an on-demand refresh.
/// </summary>
// One place answers which option sizes which allowance, so the sync service, the nightly
// loop and the tests cannot pair them differently. Each call returns a fresh budget, because
// a budget is spent down.
public sealed class EnrichmentBudgetSizes(
    IOptions<OpenLibraryOptions> openLibrary,
    IOptions<AppleBooksOptions> appleBooks)
{
    public EnrichmentBudget ForRun() => new(
        appleBooks.Value.MaximumRequestsPerRun,
        openLibrary.Value.MaximumLookupsPerRun);

    public EnrichmentBudget ForNight() => new(
        appleBooks.Value.MaximumRequestsPerNight,
        openLibrary.Value.MaximumLookupsPerNight);

    public EnrichmentBudget OnDemand() => new(
        appleBooks.Value.MaximumRequestsOnDemand,
        openLibrary.Value.MaximumLookupsOnDemand);
}
