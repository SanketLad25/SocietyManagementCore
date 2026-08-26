namespace SocietyManagementCore.Models.Entities;

/// Plain class — not ITenantScoped/IAuditable, same documented exception as ComplaintUpdate.
/// Descriptive only per the "group visit tracked as one unit" resolved decision — no independent
/// status/exit tracking per member.
public class VisitorLogMember
{
    public int VisitorLogMemberId { get; set; }
    public int SocietyId { get; set; }
    public int VisitorLogId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}
