namespace SocietyManagementCore.Models.Entities;

public class EventRegistration : ITenantScoped
{
    public int EventRegistrationId { get; set; }
    public int SocietyId { get; set; }
    public int EventId { get; set; }
    public int ResidentId { get; set; }
    public int FlatId { get; set; }
    public int ParticipantCount { get; set; } = 1;
    public string? Comments { get; set; }
    public string Status { get; set; } = EventRegistrationStatuses.Interested;
    public string RegistrationCode { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
