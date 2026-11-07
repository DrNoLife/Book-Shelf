namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>Waits for the write an action sends, rather than for the network to go quiet.</summary>
// NetworkIdle resolves at once when the save has not been sent yet: a Blazor handler that has
// not run by the time the wait starts leaves nothing in flight, so a reload could overtake the
// write. Waiting on the PUT itself, registered before the action runs, cannot miss it.
public static class PageSaveExtensions
{
    extension(IPage page)
    {
        /// <summary>
        /// Runs the action that commits an edit and waits for the PUT whose path contains
        /// <paramref name="pathPart"/> to answer, which must be a success.
        /// </summary>
        public async Task RunAndWaitForSaveAsync(Func<Task> action, string pathPart)
        {
            var response = await page.RunAndWaitForResponseAsync(
                action,
                response => response.Request.Method == "PUT"
                            && new Uri(response.Url).AbsolutePath.Contains(pathPart, StringComparison.Ordinal));

            response.Ok.ShouldBeTrue($"PUT {response.Url} answered {response.Status}.");
        }
    }
}
