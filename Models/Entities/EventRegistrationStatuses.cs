namespace SocietyManagementCore.Models.Entities;

public static class EventRegistrationStatuses
{
    public const string Interested = "Interested";
    public const string Registered = "Registered";
    public const string Waitlisted = "Waitlisted";
    public const string CancelledByResident = "CancelledByResident";
    public const string CancelledByOrganizer = "CancelledByOrganizer";

    /// Counts toward an event's capacity and toward the "active" registrant list — Interested and
    /// the cancelled variants don't hold a capacity slot (events.md).
    public const string Active = Registered;
}
