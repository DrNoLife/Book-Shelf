namespace LnShelf.Client.Features.Profile;

/// <summary>
/// The word a person types to confirm deleting their account.
/// </summary>
// Typed rather than clicked: the deletion is immediate and permanent, and a second click on a
// button that has just moved under the cursor is not a decision.
public static class DeleteConfirmation
{
    public const string Word = "delete";

    public static bool Matches(string? typed) =>
        string.Equals(typed?.Trim(), Word, StringComparison.OrdinalIgnoreCase);
}
