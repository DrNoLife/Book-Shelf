namespace LnShelf.Api.Commands;

/// <summary>
/// Runs one <see cref="IConsoleCommand"/> instead of serving requests.
/// </summary>
public static class ConsoleCommandExtensions
{
    extension(WebApplication app)
    {
        /// <returns>The process exit code.</returns>
        public async Task<int> RunConsoleCommandAsync(
            ConsoleCommandLine commandLine,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(commandLine);

            // A scope, because the commands are scoped for the same reason every
            // service here is: the DbContext.
            await using var scope = app.Services.CreateAsyncScope();
            var commands = scope.ServiceProvider.GetServices<IConsoleCommand>().ToList();

            var command = commands.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, commandLine.Name, StringComparison.OrdinalIgnoreCase));

            if (command is null)
            {
                await Console.Error.WriteLineAsync($"Unknown command '{commandLine.Name}'. Available:");

                foreach (var candidate in commands.OrderBy(candidate => candidate.Name, StringComparer.Ordinal))
                {
                    await Console.Error.WriteLineAsync($"  {candidate.Name,-16} {candidate.Description}");
                }

                return 2;
            }

            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(ConsoleCommandExtensions));

            try
            {
                var exitCode = await command.RunAsync(commandLine, cancellationToken);

                logger.LogInformation("Command {CommandName} finished with exit code {ExitCode}", command.Name, exitCode);
                return exitCode;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The one place a command's failure is turned into an exit code. Nothing
                // upstream can handle it — there is no request to answer — and a
                // stack trace on stderr is what a person running a one-shot wants.
                logger.LogError(exception, "Command {CommandName} failed", command.Name);
                return 1;
            }
        }
    }
}
