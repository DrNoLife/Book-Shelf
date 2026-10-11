using LnShelf.Client.Features.Library;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// A card prints volume numbers, not counts: with a bonus volume the count runs one past
/// the last volume, and "Vol 10" would name a volume that does not exist.
/// </summary>
public sealed class CardVolumeNumbersTests
{
    // Torture Princess: 1–9 plus the bonus 7.5, so ten volumes and the latest is Vol 9.
    private static readonly decimal[] WithBonus = [1, 2, 3, 4, 5, 6, 7, 7.5m, 8, 9];

    private static readonly decimal[] WithoutBonus = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static TheoryData<decimal[], decimal, decimal?> NewestOutCases => new()
    {
        { WithBonus, 99, 9 },
        { WithoutBonus, 99, 9 },

        // The bonus volume is the newest out when nothing after it is.
        { WithBonus, 7.5m, 7.5m },

        // Only announced volumes: nothing is new.
        { WithoutBonus, 0, null },
    };

    [Theory]
    [MemberData(nameof(NewestOutCases))]
    public void NewestOut_SeriesOutUpTo_ReturnsHighestOutNumber(decimal[] numbers, decimal outUpTo, decimal? expected)
    {
        CardVolumeNumbers.NewestOut(Ticks(numbers, outUpTo)).ShouldBe(expected);
    }

    public static TheoryData<decimal[], decimal, decimal> ForCoverCases => new()
    {
        { WithBonus, 99, 9 },
        { WithoutBonus, 8, 8 },

        // Nothing out: the newest tracked volume, as the placeholder did before.
        { WithBonus, 0, 9 },

        // Nothing tracked at all.
        { [], 0, 1 },
    };

    [Theory]
    [MemberData(nameof(ForCoverCases))]
    public void ForCover_SeriesOutUpTo_ReturnsNewestOutElseHighestTrackedElseOne(decimal[] numbers, decimal outUpTo, decimal expected)
    {
        CardVolumeNumbers.ForCover(Ticks(numbers, outUpTo)).ShouldBe(expected);
    }

    public static TheoryData<decimal[], decimal, decimal> LastMainOutCases => new()
    {
        { WithBonus, 99, 9 },
        { WithoutBonus, 99, 9 },

        // "Vol 7.5 of 7", never "of 7.5": the bonus volume is not a main one.
        { WithBonus, 7.5m, 7 },

        // Volume 9 announced only: "of 8".
        { WithBonus, 8, 8 },

        // Only a bonus volume out: it is all there is to count up to.
        { [0.5m, 1], 0.5m, 0.5m },
    };

    [Theory]
    [MemberData(nameof(LastMainOutCases))]
    public void LastMainOut_SeriesOutUpTo_ReturnsHighestMainOutNumber(decimal[] numbers, decimal outUpTo, decimal expected)
    {
        CardVolumeNumbers.LastMainOut(Ticks(numbers, outUpTo)).ShouldBe(expected);
    }

    private static List<VolumeTickResponse> Ticks(decimal[] numbers, decimal outUpTo) =>
        [.. numbers.Select(number => new VolumeTickResponse
        {
            Number = number,
            IsOut = number <= outUpTo,
            Owned = false,
            ReadStatus = ReadStatus.Unread,
        })];
}
