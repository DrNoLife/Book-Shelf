namespace LnShelf.Api.Features.Auth.Mappers;

/// <summary>
/// Makes a readable name out of an email address: "thor.christiansen@example.com" becomes
/// "Thor Christiansen".
/// </summary>
// Deliberately naive. The app holds no name from the provider, and an address is only a
// guess at one; an address that has no letters to work with falls back to itself.
public static class DisplayNameMapper
{
    private static readonly char[] WordSeparators = ['.', '_', '-', '+'];

    public static string FromEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var localPart = email.Split('@')[0];
        var words = localPart
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);

        var name = string.Join(' ', words);

        return name.Length == 0 ? email : name;
    }
}
