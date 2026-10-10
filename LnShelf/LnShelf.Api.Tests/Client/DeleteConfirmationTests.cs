using LnShelf.Client.Features.Profile;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Deleting an account needs the word typed; nothing close to it will do.
/// </summary>
public sealed class DeleteConfirmationTests
{
    [Theory]
    [InlineData("delete")]
    [InlineData("DELETE")]
    [InlineData("  Delete ")]
    public void Matches_TheWordInAnyCaseWithPadding_IsTrue(string typed) =>
        DeleteConfirmation.Matches(typed).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("del")]
    [InlineData("delete my account")]
    [InlineData("yes")]
    public void Matches_AnythingElse_IsFalse(string? typed) =>
        DeleteConfirmation.Matches(typed).ShouldBeFalse();
}
