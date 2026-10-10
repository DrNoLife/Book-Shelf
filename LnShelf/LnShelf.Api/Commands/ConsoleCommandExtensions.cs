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

            // Every command works on one user's shelf, the owner filters see to that, so the
            // command acts as the user named by --user, or as the only user when there is one.
            // Except one that looks across every shelf (cover-orphan-sweep), which has no
            // single shelf to act on.
            if (command.ActsOnOneShelf)
            {
                var userId = await ResolveUserAsync(scope.ServiceProvider, commandLine, cancellationToken);
                if (userId is { } chosen)
                {
                    scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(chosen);
                }
                else if (await scope.ServiceProvider.GetRequiredService<LnShelfDbContext>().Users.CountAsync(cancellationToken) > 1)
                {
                    await Console.Error.WriteLineAsync(
                        "There is more than one user: pass --user <email> to say whose shelf this command works on.");
                    return 2;
                }
            }

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

    /// <returns>The user --user names, or the only user; null when neither applies.</returns>
    private static async Task<Guid?> ResolveUserAsync(
        IServiceProvider services,
        ConsoleCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        var users = services.GetRequiredService<LnShelfDbContext>().Users;
        var email = commandLine.Option("user");

        if (email is not null)
        {
            var named = await users
                .Where(user => user.Email.ToLower() == email.Trim().ToLower())
                .Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return named ?? throw new InvalidOperationException($"No user has the address '{email}'.");
        }

        var all = await users.Select(user => user.Id).Take(2).ToListAsync(cancellationToken);
        return all.Count == 1 ? all[0] : null;
    }
}
