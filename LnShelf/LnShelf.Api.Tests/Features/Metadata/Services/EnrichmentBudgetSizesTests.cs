namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which option sizes which allowance, answered in one place.
/// </summary>
public sealed class EnrichmentBudgetSizesTests
{
    private static readonly EnrichmentBudgetSizes Sizes = new(
        Options.Create(new OpenLibraryOptions
        {
            MaximumLookupsPerRun = 11,
            MaximumLookupsPerNight = 12,
            MaximumLookupsOnDemand = 13,
        }),
        Options.Create(new AppleBooksOptions
        {
            MaximumRequestsPerRun = 21,
            MaximumRequestsPerNight = 22,
            MaximumRequestsOnDemand = 23,
        }));

    public static TheoryData<string, int, int> Allowances => new()
    {
        { "Run", 21, 11 },
        { "Night", 22, 12 },
        { "OnDemand", 23, 13 },
    };

    [Theory]
    [MemberData(nameof(Allowances))]
    public void Allowance_EachKind_ReadsTheMatchingNumbersOfBothSources(
        string kind, int expectedCatalogue, int expectedByIsbn)
    {
        var budget = Create(kind);

        budget.Catalogue.Remaining.ShouldBe(expectedCatalogue);
        budget.ByIsbn.Remaining.ShouldBe(expectedByIsbn);
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Night")]
    [InlineData("OnDemand")]
    public void Allowance_EveryCall_ReturnsAFreshBudget(string kind)
    {
        // A budget is spent down; handing out the same instance twice would let one run spend
        // the next one's allowance.
        var first = Create(kind);
        first.Catalogue.Spend(5);
        first.ByIsbn.Spend(5);

        var second = Create(kind);

        second.Catalogue.Remaining.ShouldBe(first.Catalogue.Remaining + 5);
        second.ByIsbn.Remaining.ShouldBe(first.ByIsbn.Remaining + 5);
    }

    private static EnrichmentBudget Create(string kind) => kind switch
    {
        "Run" => Sizes.ForRun(),
        "Night" => Sizes.ForNight(),
        "OnDemand" => Sizes.OnDemand(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
