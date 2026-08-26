using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

/// Create-only (VisitorManagement.md "DTOs") — a logged visitor's identity fields aren't editable
/// after the fact, only its status pipeline moves forward via the dedicated approve/reject/
/// check-in/check-out/cancel endpoints.
public class VisitorLogRequest
{
    [Required]
    public int FlatId { get; set; }

    [Required]
    public int VisitorCategoryId { get; set; }

    [Required, MaxLength(100)]
    public string PrimaryVisitorName { get; set; } = string.Empty;

    /// Optional — the duplicate-entry guard is skipped when this is blank (a repeat blank-mobile
    /// visit is not treated as a dupe).
    [Phone, MaxLength(15)]
    public string? PrimaryMobile { get; set; }

    /// Required only when the chosen category's RequiresVehicleNo is set (service-level check).
    [MaxLength(20)]
    public string? VehicleNo { get; set; }

    /// Required only when the chosen category's RequiresCompanyName is set (service-level check).
    [MaxLength(100)]
    public string? CompanyName { get; set; }

    public int? PartySize { get; set; }

    [MaxLength(300)]
    public string? Purpose { get; set; }

    public List<string>? MemberNames { get; set; }
}
