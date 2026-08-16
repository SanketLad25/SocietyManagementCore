namespace SocietyManagementCore.Models.Entities;

public class MaintenanceBill : ITenantScoped
{
    public int BillId { get; set; }
    public int SocietyId { get; set; }
    public int? FlatId { get; set; }
    public int? BillMonth { get; set; }
    public int? BillYear { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Status { get; set; }
    public int? CycleId { get; set; }
    public decimal? OutstandingAmount { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
