namespace SocietyManagementCore.Models.Entities;

public class Flat : ITenantScoped
{
    public int FlatId { get; set; }
    public int SocietyId { get; set; }
    public int? WingId { get; set; }
    public string? FlatNo { get; set; }
    public int? FloorNo { get; set; }
    public decimal? AreaSqFt { get; set; }
    public decimal? MaintenanceAmount { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
