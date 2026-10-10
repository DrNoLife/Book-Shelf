namespace LnShelf.Client.Features.Profile;

/// <summary>The letter on the avatar disc: there is no picture, so the name's first letter stands in.</summary>
public static class ProfileInitial
{
    public static string For(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? "?"
            : char.ToUpperInvariant(displayName.Trim()[0]).ToString();
}
