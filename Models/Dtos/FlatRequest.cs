using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class FlatRequest
{
    [Required, MaxLength(20)]
    public string FlatNo { get; set; } = string.Empty;

    public int? WingId { get; set; }

    public int? FloorNo { get; set; }

    public decimal? AreaSqFt { get; set; }

    public decimal? MaintenanceAmount { get; set; }
}
