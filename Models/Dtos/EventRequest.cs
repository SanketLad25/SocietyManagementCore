using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class EventRequest
{
    [Required]
    public int EventCategoryId { get; set; }

    [Required, MaxLength(150)]
    public string EventName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime StartOn { get; set; }

    public DateTime? EndOn { get; set; }

    [Required, MaxLength(150)]
    public string Venue { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? CoverEmoji { get; set; }

    [Required, MaxLength(150)]
    public string OrganizerName { get; set; } = string.Empty;

    public int? MaxParticipants { get; set; }

    public bool RegistrationRequired { get; set; } = true;
}
