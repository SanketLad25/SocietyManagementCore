namespace SocietyManagementCore.Models.Dtos;

public class ComplaintResponse
{
    public int ComplaintId { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public int? FlatId { get; set; }
    public string? FlatNo { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public string? AssignedToContact { get; set; }
    public string? AssignmentNotes { get; set; }
    public DateTime? AssignedOn { get; set; }
    public bool ResidentConfirmed { get; set; }
    public DateTime? ResidentConfirmedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public bool IsOwnComplaint { get; set; }
    public List<ComplaintAttachmentResponse> Attachments { get; set; } = new();
    public List<ComplaintUpdateResponse> Updates { get; set; } = new();
}
