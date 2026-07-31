namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// How many per-volume lookups one refresh may still spend.
/// </summary>
// Mutable, unlike almost everything else here, and that is the point: the budget belongs
// to the whole refresh and is spent down as each series takes its share. A record passed
// by value would give every series the full allowance, which is not a budget.
public sealed class LookupBudget(int maximum)
{
    public int Remaining { get; private set; } = Math.Max(0, maximum);

    public bool IsExhausted => Remaining is 0;

    public void Spend(int lookups) => Remaining = Math.Max(0, Remaining - lookups);
}
