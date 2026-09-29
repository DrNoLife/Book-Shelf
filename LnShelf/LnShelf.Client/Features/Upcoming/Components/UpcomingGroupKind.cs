namespace LnShelf.Client.Features.Upcoming.Components;

/// <summary>What a group's last column says about each of its lines.</summary>
public enum UpcomingGroupKind
{
    /// <summary>In print and unbought: a link to buy it.</summary>
    OutNow,

    /// <summary>Not out, with a date: the date.</summary>
    Dated,

    /// <summary>Not out, no date: says so.</summary>
    Announced,
}
