namespace LnShelf.Api.Commands;

/// <summary>
/// Splits the process arguments into a maintenance command and whatever the host
/// itself should still see.
/// </summary>
// The command name is taken out of the host's arguments deliberately. A bare word is
// not a configuration key, and handing one to the command-line configuration provider
// is asking it to guess what was meant.
public sealed record ConsoleCommandLine
{
    /// <summary>Null when the process was started to serve, which is the normal case.</summary>
    public string? Name { get; private init; }

    /// <summary>Everything after the name, for the command to read.</summary>
    public IReadOnlyList<string> Arguments { get; private init; } = [];

    /// <summary>What <c>WebApplication.CreateBuilder</c> is given.</summary>
    public string[] HostArguments { get; private init; } = [];

    public static ConsoleCommandLine Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        // A leading switch means "serve, configured like this". A leading bare word is
        // the only thing that means "run this instead of serving".
        if (arguments.Length is 0 || arguments[0].StartsWith('-') || arguments[0].StartsWith('/'))
        {
            return new ConsoleCommandLine { HostArguments = arguments };
        }

        return new ConsoleCommandLine
        {
            Name = arguments[0],
            Arguments = arguments[1..],
        };
    }

    /// <summary>
    /// The value of <c>--name value</c>, or null when the switch was not given.
    /// </summary>
    // Deliberately not a parser library: two commands with one option each do not
    // justify a dependency, and the ceremony would outweigh the feature.
    public string? Option(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var switchName = $"--{name}";

        for (var index = 0; index < Arguments.Count - 1; index++)
        {
            if (string.Equals(Arguments[index], switchName, StringComparison.OrdinalIgnoreCase))
            {
                return Arguments[index + 1];
            }
        }

        return null;
    }
}
