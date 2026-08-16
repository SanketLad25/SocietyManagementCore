using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// No-op implementation registered until a real WhatsApp (or other) provider is chosen and
/// configured (notice.md §11 item 1). Swap the DI registration in Program.cs for a real
/// implementation of INoticeBroadcastService when that happens — NoticeService.PublishAsync never
/// needs to change.
public class NoOpNoticeBroadcastService : INoticeBroadcastService
{
    public Task BroadcastAsync(Notice notice, IReadOnlyList<NoticeAttachment> attachments)
    {
        Console.WriteLine($"[NoticeBroadcast] Would broadcast notice '{notice.Title}' (NoticeId={notice.NoticeId}) " +
            $"with {attachments.Count} attachment(s) — no broadcast provider configured yet.");
        return Task.CompletedTask;
    }
}
