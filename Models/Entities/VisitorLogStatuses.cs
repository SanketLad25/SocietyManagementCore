namespace SocietyManagementCore.Models.Entities;

/// VisitorLog's status pipeline branches (PendingApproval -> Approved *or* Rejected; no-approval
/// categories skip straight to CheckedIn at creation) — unlike ComplaintStatuses.Order's single
/// linear array, this is expressed as an explicit allowed-next-states map (VisitorManagement.md
/// "Services" — "forward-only status transitions via an explicit allowed-map").
public static class VisitorLogStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string CheckedIn = "CheckedIn";
    public const string CheckedOut = "CheckedOut";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        PendingApproval, Approved, Rejected, CheckedIn, CheckedOut, Cancelled,
    };

    /// Non-terminal = still an "open" entry for the duplicate-entry guard (same SocietyId+FlatId+
    /// PrimaryMobile can't have two open entries at once).
    public static readonly string[] NonTerminal = { PendingApproval, Approved, CheckedIn };

    /// Every status this one may forward-transition into. Rejected/CheckedOut/Cancelled are
    /// terminal (no outgoing entries). Cancelled is no longer reachable from any code path — the
    /// standalone Cancel action was removed in favor of Delete covering the same "never mind,
    /// abort this entry" case (VisitorManagement.md follow-up decision) — but the constant, "All",
    /// and this status string itself stay, so historical rows already Cancelled before that
    /// change still display/filter correctly.
    public static readonly Dictionary<string, string[]> AllowedNext = new()
    {
        [PendingApproval] = new[] { Approved, Rejected },
        [Approved] = new[] { CheckedIn },
        [CheckedIn] = new[] { CheckedOut },
    };

    public static bool CanTransition(string from, string to) =>
        AllowedNext.TryGetValue(from, out var next) && next.Contains(to);
}
