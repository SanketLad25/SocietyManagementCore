using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Pluggable seam (notice.md §4/§11) for sending a published notice out via an external channel
/// (e.g. WhatsApp). Deliberately deferred — no real provider is wired up yet; NoticeService always
/// calls this on publish so a real implementation can be dropped in later via DI alone.
public interface INoticeBroadcastService
{
    Task BroadcastAsync(Notice notice, IReadOnlyList<NoticeAttachment> attachments);
}
