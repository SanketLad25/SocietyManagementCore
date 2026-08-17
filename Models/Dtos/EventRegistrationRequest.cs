using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class EventRegistrationRequest
{
    [Range(1, 100)]
    public int ParticipantCount { get; set; } = 1;

    [MaxLength(500)]
    public string? Comments { get; set; }
}
