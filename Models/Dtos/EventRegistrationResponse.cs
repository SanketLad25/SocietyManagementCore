namespace SocietyManagementCore.Models.Dtos;

public class EventRegistrationResponse
{
    public int EventRegistrationId { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public string? FlatNo { get; set; }
    public int ParticipantCount { get; set; }
    public string? Comments { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RegistrationCode { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}
