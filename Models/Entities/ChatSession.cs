namespace SocietyManagementCore.Models.Entities;

/// Plain entity — no ITenantScoped/IAuditable (SocChatBot.md "Audit-column shape, resolved
/// explicitly"): same documented exception as ComplaintUpdate/NoticeAttachment. SocietyId/UserId are
/// set by hand by ChatOrchestrationService at creation time.
///
/// UserId (not ResidentId) is the session-ownership key: AdminUserService only backfills
/// UserLogin.ResidentId for RoleNames.Resident accounts, so every other chat-eligible role (Admin,
/// Chairman, Secretary, Treasurer, Security) has ResidentId = NULL and would be indistinguishable
/// from each other on ResidentId alone. ResidentId is retained purely for tool-resolution
/// convenience (e.g. get_my_maintenance_dues), never as the "whose session is this" key.
///
/// One continuous session per (SocietyId, UserId) — no conversation-history sidebar/multiple-session
/// UX in scope (SocChatBot.md "Chat UI placement — resolved"), so ChatOrchestrationService always
/// reuses the caller's existing session rather than starting a new one each request.
public class ChatSession
{
    public int ChatSessionId { get; set; }
    public int SocietyId { get; set; }
    public int UserId { get; set; }
    public int? ResidentId { get; set; }
    public DateTime StartedAt { get; set; }
}
