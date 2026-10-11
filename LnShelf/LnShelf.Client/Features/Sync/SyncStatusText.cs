namespace LnShelf.Client.Features.Sync;

/// <summary>
/// The sidebar's sync note: one short line for the column, the details for its tooltip.
/// </summary>
public static class SyncStatusText
{
    public const string NeverSyncedHeadline = "Not synced yet";

    public const string NeverSyncedDetail =
        "Your shelf is refreshed against the release source every night. "
        + "The first run shows up here once your shelf has series to refresh.";

    // A run that has been "in progress" for longer than a sweep plausibly takes is a run
    // that died with the process, and must not claim to be working forever.
    private static readonly TimeSpan StaleRunAge = TimeSpan.FromHours(2);

    extension(SyncSummaryResponse run)
    {
        /// <summary>"Synced 3 h ago": a relative age, because a bare clock time says nothing about which day.</summary>
        public string Headline(DateTimeOffset now)
        {
            if (run.FinishedAt is not { } finishedAt)
            {
                return now - run.StartedAt < StaleRunAge ? "Sync in progress" : "Last sync did not finish";
            }

            var age = now - finishedAt;

            return age switch
            {
                { TotalMinutes: < 1 } => "Synced just now",
                { TotalHours: < 1 } => $"Synced {(int)age.TotalMinutes} min ago",
                { TotalDays: < 1 } => $"Synced {(int)age.TotalHours} h ago",
                _ => $"Synced {(int)age.TotalDays} d ago",
            };
        }

        /// <summary>
        /// The line under the headline, present only when something is waiting on the user.
        /// Failures are here rather than in the tooltip because a hover is not reachable by touch.
        /// </summary>
        public string? Attention
        {
            get
            {
                var toReview = run.Discrepancies.Count(discrepancy => discrepancy.NeedsReview);

                List<string> parts = [];
                if (run.Failures > 0) parts.Add($"{run.Failures} failed");
                if (toReview > 0) parts.Add($"{toReview} to review");

                return parts.Count > 0 ? string.Join(" · ", parts) : null;
            }
        }

        /// <summary>
        /// " · 2 failed", or nothing: the half of <see cref="Attention"/> that is not a link. The
        /// "to review" half is printed as a link to the review list where there is room for one.
        /// </summary>
        public string? FailureClause => run.Failures > 0 ? $" · {run.Failures} failed" : null;

        /// <summary>
        /// The dot beside the note: amber when something is waiting or the run died, quiet while
        /// it is still going, green otherwise.
        /// </summary>
        public SignalTone Tone(DateTimeOffset now)
        {
            if (run.Attention is not null)
            {
                return SignalTone.Attention;
            }

            if (run.FinishedAt is null)
            {
                return now - run.StartedAt < StaleRunAge ? SignalTone.Quiet : SignalTone.Attention;
            }

            return SignalTone.Ok;
        }

        /// <summary>The run's own words and the exact time, for the tooltip.</summary>
        // The summary already ends with the failure count, so it is not appended again here.
        public string Detail =>
            $"{run.StartedAt.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture)} · {run.Summary}";
    }
}
