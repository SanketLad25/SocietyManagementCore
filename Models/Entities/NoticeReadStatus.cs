namespace SocietyManagementCore.Models.Entities;

/// Plain class — implements neither ITenantScoped nor IAuditable (notice.md §2.3, same documented
/// exception as MaintenanceBillLineItem/NoticeAttachment). One row = "this user has read/dismissed
/// this notice" — an immutable fact stamped once, never updated.
public class NoticeReadStatus
{
    public int NoticeReadStatusId { get; set; }
    public int SocietyId { get; set; }
    public int NoticeId { get; set; }
    public int UserId { get; set; }
    public DateTime ReadOn { get; set; }
}
