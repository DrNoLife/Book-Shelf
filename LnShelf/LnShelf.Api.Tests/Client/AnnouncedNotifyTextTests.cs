using LnShelf.Client.Features.Upcoming;

namespace LnShelf.Api.Tests.Client;

/// <summary>The words on the Announced line's notification control.</summary>
public sealed class AnnouncedNotifyTextTests
{
    [Theory]
    [InlineData(false, "Notify me when dated")]
    [InlineData(true, "We'll tell you when dated")]
    public void Label_OffOrOn_SaysTheActionOrThePromise(bool on, string expected) =>
        AnnouncedNotifyText.Label(on).ShouldBe(expected);
}
