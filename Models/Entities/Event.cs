namespace SocietyManagementCore.Models.Entities;

public class Event : ITenantScoped
{
    public int EventId { get; set; }
    public int SocietyId { get; set; }
    public int EventCategoryId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartOn { get; set; }
    public DateTime? EndOn { get; set; }
    public string Venue { get; set; } = string.Empty;
    public string? CoverEmoji { get; set; }
    public string? CoverImagePath { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public int? MaxParticipants { get; set; }
    public bool RegistrationRequired { get; set; } = true;
    public string Status { get; set; } = EventStatuses.Published;
    public string? CancelReason { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
