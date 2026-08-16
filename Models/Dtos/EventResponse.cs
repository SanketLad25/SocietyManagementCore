namespace SocietyManagementCore.Models.Dtos;

public class EventResponse
{
    public int EventId { get; set; }
    public int EventCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartOn { get; set; }
    public DateTime? EndOn { get; set; }
    public string Venue { get; set; } = string.Empty;
    public string? CoverEmoji { get; set; }
    public string? CoverImagePath { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public int? MaxParticipants { get; set; }
    public bool RegistrationRequired { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CancelReason { get; set; }
    public DateTime CreatedOn { get; set; }
}
