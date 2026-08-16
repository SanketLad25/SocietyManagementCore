using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ParkingTypeRequest
{
    [Required, MaxLength(50)]
    public string TypeName { get; set; } = string.Empty;

    public int? DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
