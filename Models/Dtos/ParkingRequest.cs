using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ParkingRequest
{
    [Required]
    public int FlatId { get; set; }

    /// The parking category this slot/vehicle counts against for billing (e.g. Two-Wheeler,
    /// Four-Wheeler) — see MaintenanceTargetResolver/PerUnitStrategy, which count rows by this.
    [Required]
    public int ParkingTypeId { get; set; }

    [MaxLength(20)]
    public string? ParkingNo { get; set; }

    [MaxLength(20)]
    public string? VehicleNo { get; set; }

    /// Free-text sub-classification (e.g. "Scooter", "Sedan") — distinct from ParkingTypeId, which
    /// is the billing category.
    [MaxLength(20)]
    public string? VehicleType { get; set; }
}
