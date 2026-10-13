using LnShelf.Api.Commands;

namespace LnShelf.Api.Tests.Commands;

/// <summary>
/// The first decision the process makes: serve, or run a one-shot job instead — and the
/// option values those jobs read their paths and filters from.
/// </summary>
// Program.cs branches on Name and hands HostArguments to WebApplication.CreateBuilder, so a
// wrong answer here is either a maintenance job that never runs or a bare word offered to the
// configuration provider as a setting. CompositionRootTests covers the command *names*;
// nothing covered the parsing until this file.
public sealed class ConsoleCommandLineTests
{
    [Fact]
    public void Parse_NoArguments_Serves()
    {
        var commandLine = ConsoleCommandLine.Parse([]);

        commandLine.Name.ShouldBeNull();
        commandLine.Arguments.ShouldBeEmpty();
        commandLine.HostArguments.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--urls")]
    [InlineData("-v")]
    [InlineData("/urls")]
    public void Parse_ALeadingSwitch_ServesAndLeavesEveryArgumentToTheHost(string leading)
    {
        var commandLine = ConsoleCommandLine.Parse([leading, "http://localhost:5080"]);

        // A switch means "serve, configured like this", so the host must still see all of it.
        commandLine.Name.ShouldBeNull();
        commandLine.HostArguments.ShouldBe([leading, "http://localhost:5080"]);
    }

    [Fact]
    public void Parse_AnUnknownLeadingSwitch_IsStillTheHostsProblemAndNotACommand()
    {
        // The switch is meaningless to the configuration provider too, but that is its
        // decision to make: a leading `--` is never a command name.
        var commandLine = ConsoleCommandLine.Parse(["--out", "isbn-review.csv"]);

        commandLine.Name.ShouldBeNull();
        commandLine.HostArguments.ShouldBe(["--out", "isbn-review.csv"]);
    }

    [Fact]
    public void Parse_ALeadingBareWord_IsTheCommandName()
    {
        var commandLine = ConsoleCommandLine.Parse(["isbn-backfill"]);

        commandLine.Name.ShouldBe("isbn-backfill");
        commandLine.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_ACommandWithOptions_KeepsTheOptionsAndOffersTheHostNothing()
    {
        var commandLine = ConsoleCommandLine.Parse(["isbn-discover", "--out", "../../docs/isbn-review.csv"]);

        commandLine.Name.ShouldBe("isbn-discover");
        commandLine.Arguments.ShouldBe(["--out", "../../docs/isbn-review.csv"]);

        // The whole reason the name is taken out: handing a bare word to the command-line
        // configuration provider is asking it to guess what was meant.
        commandLine.HostArguments.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("out", "../../docs/isbn-review.csv")]
    [InlineData("in", "../../docs/isbn-review.csv")]
    [InlineData("series", "Overlord;86―EIGHTY-SIX")]
    [InlineData("format", "LightNovel")]
    public void Option_ASwitchWithAValue_ReturnsTheValue(string name, string value)
    {
        var commandLine = ConsoleCommandLine.Parse(["enrichment-verify", $"--{name}", value]);

        // A semicolon-separated list is one argument, not several: the shell quotes it and
        // the command splits it, so nothing here needs to know it is a list.
        commandLine.Option(name).ShouldBe(value);
    }

    [Fact]
    public void Option_SeveralSwitches_ReadsEachIndependently()
    {
        var commandLine = ConsoleCommandLine.Parse(
            ["enrichment-verify", "--series", "Overlord", "--format", "LightNovel"]);

        commandLine.Option("series").ShouldBe("Overlord");
        commandLine.Option("format").ShouldBe("LightNovel");
    }

    [Fact]
    public void Option_ASwitchThatWasNotGiven_ReturnsNull()
    {
        var commandLine = ConsoleCommandLine.Parse(["isbn-discover"]);

        // Every caller reads this as "use the default", so null has to mean absent and
        // nothing else (IsbnDiscoverCommand.DefaultPath).
        commandLine.Option("out").ShouldBeNull();
    }

    [Fact]
    public void Option_ASwitchTypedInAnotherCasing_StillMatches()
    {
        var commandLine = ConsoleCommandLine.Parse(["isbn-apply", "--IN", "review.csv"]);

        commandLine.Option("in").ShouldBe("review.csv");
    }

    [Fact]
    public void Option_TheSameSwitchTwice_ReturnsTheFirst()
    {
        var commandLine = ConsoleCommandLine.Parse(["isbn-apply", "--in", "first.csv", "--in", "second.csv"]);

        // First wins because the scan stops at the first match. Nobody types this on purpose;
        // it is pinned so a later rewrite cannot flip it silently.
        commandLine.Option("in").ShouldBe("first.csv");
    }

    [Fact]
    public void Option_ASwitchAtTheEndWithNoValue_Throws()
    {
        var commandLine = ConsoleCommandLine.Parse(["isbn-discover", "--out"]);

        // A switch with nothing after it is a mistake, not "not given": reading it as the
        // latter made IsbnDiscoverCommand write to its DefaultPath instead of where the
        // caller meant. The central handler turns this into a logged error and exit code 1.
        var exception = Should.Throw<InvalidOperationException>(() => commandLine.Option("out"));

        exception.Message.ShouldContain("--out");
    }

    [Fact]
    public void Option_ASwitchAtTheEndWithNoValue_OtherSwitchesStillReadable()
    {
        var commandLine = ConsoleCommandLine.Parse(["enrichment-verify", "--series", "X", "--format"]);

        commandLine.Option("series").ShouldBe("X");
        Should.Throw<InvalidOperationException>(() => commandLine.Option("format"));
    }

    [Fact]
    public void Option_ASwitchFollowedByAnotherSwitch_TakesThatSwitchAsItsValue()
    {
        var commandLine = ConsoleCommandLine.Parse(["enrichment-verify", "--series", "--format", "LightNovel"]);

        // Also pinned rather than endorsed. Nothing here knows which switches take a value,
        // so "the next token" is the only rule available to it.
        commandLine.Option("series").ShouldBe("--format");
    }

    [Fact]
    public void Option_ASwitchGivenBeforeTheCommandName_IsNotFound()
    {
        // Not reachable through Parse — a leading switch means serve — but it states the
        // shape the commands are written against: options come after the name.
        var commandLine = ConsoleCommandLine.Parse(["--out", "review.csv", "isbn-discover"]);

        commandLine.Name.ShouldBeNull();
        commandLine.Option("out").ShouldBeNull();
    }
}
