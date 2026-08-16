using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class AmenitySubscriptionRequest
{
    [Required]
    public int FlatId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }
}
