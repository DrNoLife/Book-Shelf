using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Search;

namespace LnShelf.Client.Features.Recommendations;

/// <summary>
/// The Add on every recommendation tile of a page. Shared so only one Add runs at a time
/// across all rows: only that button ticks, the rest are disabled quietly (design.md, Busy states).
/// </summary>
public sealed class RecommendationAddState(SeriesAddClient addClient, LibraryState libraryState)
{
    private readonly Dictionary<int, Guid> _added = [];

    /// <summary>Which work's add is in flight.</summary>
    public int? AddingAniListId { get; private set; }

    public bool IsAdding => AddingAniListId is not null;

    public string? Message { get; private set; }

    /// <summary>Works added this visit, to the series they became.</summary>
    public IReadOnlyDictionary<int, Guid> Added => _added;

    public event Action? Changed;

    public void ClearMessage()
    {
        Message = null;
        Changed?.Invoke();
    }

    /// <summary>The search's own add: nothing read or owned. The tile then links to the new series.</summary>
    public async Task AddAsync(RecommendationResponse work)
    {
        if (IsAdding)
        {
            return;
        }

        AddingAniListId = work.AniListId;
        Message = null;
        Changed?.Invoke();

        try
        {
            var outcome = await addClient.AddAsync(
                new AddSeriesRequest { AniListId = work.AniListId, Format = work.Format },
                work.TitleEnglish,
                CancellationToken.None);

            Message = outcome.Message;

            if (outcome.Added is not { } added)
            {
                return;
            }

            _added[work.AniListId] = added.SeriesId;

            if (libraryState.Library is not null)
            {
                await libraryState.LoadAsync();
            }
        }
        finally
        {
            AddingAniListId = null;
            Changed?.Invoke();
        }
    }
}
