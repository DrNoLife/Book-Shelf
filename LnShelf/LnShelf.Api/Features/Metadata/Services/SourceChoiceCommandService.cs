namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// No external calls, so no rate-limit policy: a choice is applied from what the diff already
// showed, and the nightly sync keeps it fresh afterwards.
public sealed class SourceChoiceCommandService(
    LnShelfDbContext dbContext,
    SourceChoiceApplier applier,
    ISeriesDetailQueryService seriesDetailQueryService,
    TimeProvider timeProvider) : ISourceChoiceCommandService
{
    public async Task<SeriesDetailResponse?> ChooseAsync(
        Guid seriesId,
        ChooseSourceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureChoosable(request.Field, request.VolumeNumber);

        if (!Enum.IsDefined(request.Source))
        {
            throw new InvalidInputException("That is not a source the shelf knows.");
        }

        var series = await LoadAsync(seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        if (request.VolumeNumber is { } number && series.Volumes.All(volume => volume.Number != number))
        {
            throw new InvalidInputException($"This series has no volume {number}.");
        }

        var recorded = await dbContext.SyncRuns.NewestDisagreementAsync(
            seriesId, request.Field, request.VolumeNumber, cancellationToken);

        if (recorded is null || recorded.Discrepancy.Values.All(value => value.Source != request.Source))
        {
            throw new InvalidInputException("That source said nothing about this, so there is nothing to choose.");
        }

        var existing = series.ChoiceFor(request.Field, request.VolumeNumber);

        // Changing a choice puts the old one back first, so the new one starts from the
        // automatic state and not from the old one's effect.
        if (existing is not null)
        {
            applier.Revert(series, existing, recorded.Discrepancy);
        }

        var choice = existing ?? new SourceChoice
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            Field = request.Field,
            VolumeNumber = request.VolumeNumber,
        };

        choice.Source = request.Source;
        choice.ReplacedValue = null;
        choice.ChosenAt = timeProvider.GetUtcNow();

        if (existing is null)
        {
            series.SourceChoices.Add(choice);
        }

        applier.Apply(series, choice, recorded.Discrepancy);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await seriesDetailQueryService.GetAsync(seriesId, cancellationToken);
    }

    public async Task<SeriesDetailResponse?> ResetAsync(
        Guid seriesId,
        SyncDiscrepancyField field,
        int? volumeNumber,
        CancellationToken cancellationToken)
    {
        EnsureChoosable(field, volumeNumber);

        var series = await LoadAsync(seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        if (series.ChoiceFor(field, volumeNumber) is { } choice)
        {
            var recorded = await dbContext.SyncRuns.NewestDisagreementAsync(
                seriesId, field, volumeNumber, cancellationToken);

            applier.Revert(series, choice, recorded?.Discrepancy);

            // Only the choice goes. Every recorded disagreement stays: the log is a record
            // of what the sources said, not of what the user did about it.
            dbContext.SourceChoices.Remove(choice);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await seriesDetailQueryService.GetAsync(seriesId, cancellationToken);
    }

    private static void EnsureChoosable(SyncDiscrepancyField field, int? volumeNumber)
    {
        if (!Enum.IsDefined(field) || field is SyncDiscrepancyField.SeriesLink)
        {
            throw new InvalidInputException(
                "Only the volume count, a volume's release date and the publication status can be chosen.");
        }

        var isPerVolume = field is SyncDiscrepancyField.ReleaseDate;

        if (isPerVolume && volumeNumber is null)
        {
            throw new InvalidInputException("A release date is chosen per volume, so name the volume.");
        }

        if (!isPerVolume && volumeNumber is not null)
        {
            throw new InvalidInputException("This field belongs to the whole series, so leave the volume out.");
        }
    }

    // Tracked, because the applier writes through it. Through dbContext.Series, which is
    // filtered by owner: another user's id comes back null and the controller answers 404.
    private Task<Series?> LoadAsync(Guid seriesId, CancellationToken cancellationToken) =>
        dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.SourceChoices)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);
}
