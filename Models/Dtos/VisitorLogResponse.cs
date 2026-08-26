namespace SocietyManagementCore.Models.Dtos;

public class VisitorLogResponse
{
    public int VisitorLogId { get; set; }
    public int FlatId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public int VisitorCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string PrimaryVisitorName { get; set; } = string.Empty;
    public string? PrimaryMobile { get; set; }
    public string? VehicleNo { get; set; }
    public string? CompanyName { get; set; }
    public int PartySize { get; set; }
    public string? Purpose { get; set; }
    public bool ApprovalRequired { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public string LoggedByName { get; set; } = string.Empty;
    public string? CheckedOutByName { get; set; }
    public DateTime CreatedOn { get; set; }
    public List<string> MemberNames { get; set; } = new();
    public List<VisitorAttachmentResponse> Attachments { get; set; } = new();

    /// Server-computed per caller (VisitorManagement.md "DTOs") — avoids duplicating the
    /// authorization matrix in frontend JS. The frontend drives row/detail actions off these
    /// booleans instead of re-deriving role logic.
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanCheckIn { get; set; }
    public bool CanCheckOut { get; set; }
    public bool CanDelete { get; set; }
}
