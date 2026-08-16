namespace SocietyManagementCore.Models.Entities;

public class Complaint : ITenantScoped
{
    public int ComplaintId { get; set; }
    public int SocietyId { get; set; }
    public int ResidentId { get; set; }
    public int? FlatId { get; set; }
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = ComplaintPriorities.Medium;
    public string Status { get; set; } = ComplaintStatuses.Open;
    public string? AssignedToName { get; set; }
    public string? AssignedToContact { get; set; }
    public string? AssignmentNotes { get; set; }
    public int? AssignedBy { get; set; }
    public DateTime? AssignedOn { get; set; }
    public bool ResidentConfirmed { get; set; }
    public DateTime? ResidentConfirmedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
