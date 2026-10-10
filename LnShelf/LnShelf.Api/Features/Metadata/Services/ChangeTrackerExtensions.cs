using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Throws away whatever the change tracker holds that has not been saved.
/// </summary>
// The sync saves once per series, so "not saved yet" is exactly the work of the series that
// just failed. Without this, a series that threw halfway kept its half-applied changes
// tracked, and the next series' save wrote them as if they had succeeded.
//
// Reverted in place rather than with ChangeTracker.Clear(): the run row and the series still
// waiting their turn are tracked in the same context, and clearing would detach them too.
public static class ChangeTrackerExtensions
{
    extension(ChangeTracker changeTracker)
    {
        public void DiscardPendingChanges()
        {
            // Entries() runs DetectChanges first, so a volume only added to a collection, or
            // a counter only incremented on the run, shows up here as pending.
            var entries = changeTracker.Entries().ToList();

            var added = entries
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .ToHashSet(ReferenceEqualityComparer.Instance);

            // Unhooked from the entities that stay tracked before being detached. Detaching
            // alone leaves a new volume sitting in its series' collection, and the next
            // DetectChanges would find it there and add it straight back.
            foreach (var entry in entries.Where(entry => entry.State != EntityState.Added))
            {
                UnhookAdded(entry, added);
            }

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;

                    case EntityState.Modified or EntityState.Deleted:
                        entry.CurrentValues.SetValues(entry.OriginalValues);
                        entry.State = EntityState.Unchanged;
                        break;
                }
            }
        }
    }

    private static void UnhookAdded(EntityEntry entry, HashSet<object> added)
    {
        foreach (var collection in entry.Collections)
        {
            if (collection.CurrentValue is null)
            {
                continue;
            }

            var accessor = collection.Metadata.GetCollectionAccessor();
            var toRemove = collection.CurrentValue.Cast<object>().Where(added.Contains).ToList();

            foreach (var item in toRemove)
            {
                accessor?.Remove(entry.Entity, item);
            }
        }

        foreach (var reference in entry.References)
        {
            if (reference.CurrentValue is { } target && added.Contains(target))
            {
                reference.CurrentValue = null;
            }
        }
    }
}
