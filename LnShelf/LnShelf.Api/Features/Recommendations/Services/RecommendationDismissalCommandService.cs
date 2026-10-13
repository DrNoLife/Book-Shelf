namespace LnShelf.Api.Features.Recommendations.Services;

/// <inheritdoc />
public sealed class RecommendationDismissalCommandService(
    LnShelfDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<RecommendationDismissalCommandService> logger) : IRecommendationDismissalCommandService
{
    /// <summary>More than anyone dismisses; caps a script.</summary>
    public const int MaximumDismissalsPerUser = 1000;

    public async Task DismissAsync(int aniListId, DismissRecommendationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (aniListId <= 0)
        {
            throw new InvalidInputException("That is not a catalogue id.");
        }

        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            throw new InvalidInputException("A dismissed work needs its title.");
        }

        if (title.Length > RecommendationDismissal.TitleMaximumLength)
        {
            throw new InvalidInputException(
                $"A title is at most {RecommendationDismissal.TitleMaximumLength} characters.");
        }

        if (!Enum.IsDefined(request.Format))
        {
            throw new InvalidInputException("That is not a format this shelf tracks.");
        }

        // Already hidden: keep the row, and with it the first DismissedAt.
        if (await dbContext.RecommendationDismissals.AnyAsync(candidate => candidate.AniListId == aniListId, cancellationToken))
        {
            return;
        }

        if (await dbContext.RecommendationDismissals.CountAsync(cancellationToken) >= MaximumDismissalsPerUser)
        {
            throw new ConflictException($"You have hidden {MaximumDismissalsPerUser} suggestions already. Show some again first.");
        }

        dbContext.RecommendationDismissals.Add(new RecommendationDismissal
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            Title = title,
            Format = request.Format,
            DismissedAt = timeProvider.GetUtcNow(),
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation)
        {
            // A double click raced itself: the other request stored it, which is the outcome asked for.
            return;
        }

        logger.LogInformation("User {UserId} is not interested in {AniListId}", currentUser.Id, aniListId);
    }

    public async Task<bool> RestoreAsync(int aniListId, CancellationToken cancellationToken)
    {
        var dismissal = await dbContext.RecommendationDismissals
            .FirstOrDefaultAsync(candidate => candidate.AniListId == aniListId, cancellationToken);

        if (dismissal is null)
        {
            return false;
        }

        dbContext.RecommendationDismissals.Remove(dismissal);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} wants {AniListId} suggested again", currentUser.Id, aniListId);
        return true;
    }
}
