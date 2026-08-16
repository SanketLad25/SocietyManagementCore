namespace SocietyManagementCore.Models.Entities;

public static class ComplaintStatuses
{
    public const string Open = "Open";
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    /// Defines the only forward transitions ComplaintService.UpdateStatusAsync will accept — no
    /// skipping, no going backward (complaints.md §4/§7).
    public static readonly string[] Order = { Open, Assigned, InProgress, Resolved, Closed };

    public static readonly string[] All = Order;
}
