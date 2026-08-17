using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class EventCancelRequest
{
    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
