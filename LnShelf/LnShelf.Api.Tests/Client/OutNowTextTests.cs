using LnShelf.Client.Features.Library;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The phone's "Out now" line may only print a range when the volumes to buy are one.
/// </summary>
public sealed class OutNowTextTests
{
    public static TheoryData<int[], string> Cases => new()
    {
        { [21], "Vol 21 is out" },
        { [21, 22], "Vol 21–22 are out" },
        { [4, 5, 6], "Vol 4–6 are out" },

        // Volume 5 is owned, so 4–6 would claim a volume the reader already has.
        { [4, 6], "2 out, from Vol 4" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void For_VolumesToBuy_PrintsARangeOnlyWhenTheyAreContiguous(int[] toBuy, string expected)
    {
        var ticks = Enumerable.Range(1, 22)
            .Select(number => new VolumeTickResponse
            {
                Number = number,
                IsOut = true,
                Owned = !toBuy.Contains(number),
                ReadStatus = ReadStatus.Unread,
            })
            .ToList();

        var card = new LibraryCardResponse
        {
            SeriesId = Guid.NewGuid(),
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            TrackingStatus = TrackingStatus.Reading,
            HasNewVolume = false,
            Read = 0,
            Owned = 22 - toBuy.Length,
            Published = 22,
            ToBuy = toBuy.Length,
            Backlog = 0,
            Tracked = 22,
            VolumeTicks = ticks,
        };

        OutNowText.For(new OutNowEntry(card, toBuy.Min())).ShouldBe(expected);
    }
}
