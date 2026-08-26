using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class NoticeService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    AuditLogService auditLog,
    INoticeBroadcastService broadcastService,
    NoticeAttachmentService attachmentService,
    IngestionService ingestionService,
    ILogger<NoticeService> logger)
{
    private static readonly string[] ManagerRoles = RoleNames.NoticeManagerRoles.Split(',');

    public async Task<List<NoticeResponse>> ListAsync(int? categoryId, string? priority, string? search, string? sortBy, bool includeDrafts)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<NoticeResponse>();
        }

        var query = db.Notices.Where(n => n.SocietyId == societyId);
        query = includeDrafts && IsManager() ? query : query.Where(n => n.Status == NoticeStatuses.Published);

        if (categoryId is { } cid)
        {
            query = query.Where(n => n.CategoryId == cid);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(n => n.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Description.Contains(term));
        }

        query = sortBy switch
        {
            "priority" => query
                .OrderBy(n => n.Priority == NoticePriorities.Urgent ? 0 : n.Priority == NoticePriorities.Important ? 1 : 2)
                .ThenByDescending(n => n.PublishDate),
            "expiry" => query.OrderBy(n => n.ExpiryDate ?? DateTime.MaxValue),
            _ => query.OrderByDescending(n => n.PublishDate),
        };

        var notices = await query.ToListAsync();
        return await ToResponsesAsync(notices);
    }

    public async Task<NoticeResponse?> GetAsync(int noticeId)
    {
        var notice = await FindInSocietyAsync(noticeId);
        if (notice is null || (notice.Status != NoticeStatuses.Published && !IsManager()))
        {
            return null;
        }

        var responses = await ToResponsesAsync(new List<Notice> { notice });
        return responses.FirstOrDefault();
    }

    public async Task<ServiceResult<NoticeResponse>> CreateAsync(NoticeRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<NoticeResponse>.Fail("No society context for this account.");
        }

        var validationError = await ValidateAsync(societyId, request);
        if (validationError is not null)
        {
            return ServiceResult<NoticeResponse>.Fail(validationError);
        }

        var notice = new Notice
        {
            SocietyId = societyId,
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Priority = request.Priority,
            Status = NoticeStatuses.Draft,
            PublishDate = request.PublishDate,
            ExpiryDate = request.ExpiryDate,
        };

        db.Notices.Add(notice);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "Notice", notice.NoticeId, notice.Title);

        var responses = await ToResponsesAsync(new List<Notice> { notice });
        return ServiceResult<NoticeResponse>.Ok(responses[0]);
    }

    public async Task<ServiceResult<NoticeResponse>> UpdateAsync(int noticeId, NoticeRequest request)
    {
        var notice = await FindInSocietyAsync(noticeId);
        if (notice is null)
        {
            return ServiceResult<NoticeResponse>.Fail("Notice not found.");
        }

        if (notice.Status == NoticeStatuses.Published)
        {
            return ServiceResult<NoticeResponse>.Fail("Cannot edit a published notice — delete it and create a new one instead.");
        }

        var validationError = await ValidateAsync(notice.SocietyId, request);
        if (validationError is not null)
        {
            return ServiceResult<NoticeResponse>.Fail(validationError);
        }

        notice.Title = request.Title;
        notice.Description = request.Description;
        notice.CategoryId = request.CategoryId;
        notice.Priority = request.Priority;
        notice.PublishDate = request.PublishDate;
        notice.ExpiryDate = request.ExpiryDate;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "Notice", notice.NoticeId, notice.Title);

        var responses = await ToResponsesAsync(new List<Notice> { notice });
        return ServiceResult<NoticeResponse>.Ok(responses[0]);
    }

    public async Task<ServiceResult<NoticeResponse>> PublishAsync(int noticeId)
    {
        var notice = await FindInSocietyAsync(noticeId);
        if (notice is null)
        {
            return ServiceResult<NoticeResponse>.Fail("Notice not found.");
        }

        if (notice.Status == NoticeStatuses.Published)
        {
            return ServiceResult<NoticeResponse>.Fail("This notice has already been published.");
        }

        notice.Status = NoticeStatuses.Published;
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Publish", "Notice", notice.NoticeId, notice.Title);

        // SocChatBot.md Fix 1: Draft -> Published is one-way (NoticeStatuses.cs) — this is the only
        // "published status changed" hook Notice has, so it's where ingestion must be wired. A
        // notice must be searchable immediately on publish, not only after the next admin-triggered
        // IngestAllPublishedAsync run. An ingestion failure must not roll back the publish that
        // already succeeded — log it and move on.
        var ingestResult = await ingestionService.IngestNoticeAsync(notice.SocietyId, notice);
        if (!ingestResult.Success)
        {
            logger.LogWarning(
                "Ingestion failed for published Notice {NoticeId} in society {SocietyId}: {Error}",
                notice.NoticeId, notice.SocietyId, ingestResult.Error);
        }

        var attachments = await db.NoticeAttachments.Where(a => a.NoticeId == notice.NoticeId).ToListAsync();
        await broadcastService.BroadcastAsync(notice, attachments);

        var responses = await ToResponsesAsync(new List<Notice> { notice });
        return ServiceResult<NoticeResponse>.Ok(responses[0]);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int noticeId)
    {
        var notice = await FindInSocietyAsync(noticeId);
        if (notice is null)
        {
            return ServiceResult<bool>.Fail("Notice not found.");
        }

        var attachments = await db.NoticeAttachments.Where(a => a.NoticeId == noticeId).ToListAsync();
        foreach (var attachment in attachments)
        {
            attachmentService.DeletePhysicalFile(attachment.StoragePath);
        }
        db.NoticeAttachments.RemoveRange(attachments);

        var readStatuses = await db.NoticeReadStatuses.Where(r => r.NoticeId == noticeId).ToListAsync();
        db.NoticeReadStatuses.RemoveRange(readStatuses);

        db.Notices.Remove(notice);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Delete", "Notice", noticeId, notice.Title);

        // SocChatBot.md Fix 1: a deleted notice (published or not) must not leave stale, still-
        // searchable chunks behind — no-op if the notice was never published (no chunks exist). A
        // failure here (e.g. a transient DB blip) must not roll back the notice delete that already
        // succeeded — log it and move on, same as an ingestion failure on publish already does.
        var removeResult = await ingestionService.RemoveSourceChunksAsync(notice.SocietyId, DocumentChunkSourceTypes.Notice, noticeId);
        if (!removeResult.Success)
        {
            logger.LogWarning(
                "Removing search index chunks failed for deleted Notice {NoticeId} in society {SocietyId}: {Error}",
                noticeId, notice.SocietyId, removeResult.Error);
        }

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> MarkReadAsync(int noticeId)
    {
        if (currentUser.SocietyId is not { } societyId || currentUser.UserId is not { } userId)
        {
            return ServiceResult<bool>.Fail("Not authenticated.");
        }

        var noticeExists = await db.Notices.AnyAsync(n => n.NoticeId == noticeId && n.SocietyId == societyId);
        if (!noticeExists)
        {
            return ServiceResult<bool>.Fail("Notice not found.");
        }

        var alreadyRead = await db.NoticeReadStatuses.AnyAsync(r => r.NoticeId == noticeId && r.UserId == userId);
        if (!alreadyRead)
        {
            db.NoticeReadStatuses.Add(new NoticeReadStatus
            {
                SocietyId = societyId,
                NoticeId = noticeId,
                UserId = userId,
                ReadOn = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        return ServiceResult<bool>.Ok(true);
    }

    private async Task<string?> ValidateAsync(int societyId, NoticeRequest request)
    {
        if (!NoticePriorities.All.Contains(request.Priority))
        {
            return "Invalid priority.";
        }

        var categoryValid = await db.NoticeCategories.AnyAsync(c =>
            c.SocietyId == societyId && c.NoticeCategoryId == request.CategoryId && c.IsActive);
        if (!categoryValid)
        {
            return "Invalid category.";
        }

        if (request.ExpiryDate is { } expiry && expiry < request.PublishDate)
        {
            return "Expiry date must be on or after the publish date.";
        }

        return null;
    }

    private bool IsManager() => currentUser.RoleName is { } role && ManagerRoles.Contains(role);

    private Task<Notice?> FindInSocietyAsync(int noticeId) =>
        currentUser.SocietyId is { } societyId
            ? db.Notices.FirstOrDefaultAsync(n => n.NoticeId == noticeId && n.SocietyId == societyId)
            : Task.FromResult<Notice?>(null);

    private async Task<List<NoticeResponse>> ToResponsesAsync(List<Notice> notices)
    {
        if (notices.Count == 0)
        {
            return new List<NoticeResponse>();
        }

        var categoryIds = notices.Select(n => n.CategoryId).Distinct().ToList();
        var categoryNames = await db.NoticeCategories
            .Where(c => categoryIds.Contains(c.NoticeCategoryId))
            .ToDictionaryAsync(c => c.NoticeCategoryId, c => c.CategoryName);

        var createdByIds = notices.Where(n => n.CreatedBy.HasValue).Select(n => n.CreatedBy!.Value).Distinct().ToList();
        var createdByNames = await ResolveDisplayNamesAsync(createdByIds);

        var noticeIds = notices.Select(n => n.NoticeId).ToList();
        var readOnByNoticeId = new Dictionary<int, DateTime>();
        if (currentUser.UserId is { } userId)
        {
            readOnByNoticeId = await db.NoticeReadStatuses
                .Where(r => noticeIds.Contains(r.NoticeId) && r.UserId == userId)
                .ToDictionaryAsync(r => r.NoticeId, r => r.ReadOn);
        }

        var attachmentsByNoticeId = await db.NoticeAttachments
            .Where(a => noticeIds.Contains(a.NoticeId))
            .GroupBy(a => a.NoticeId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(a => new NoticeAttachmentResponse
                {
                    NoticeAttachmentId = a.NoticeAttachmentId,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSizeBytes = a.FileSizeBytes,
                    UploadedOn = a.UploadedOn,
                }).ToList());

        var now = DateTime.UtcNow;

        return notices.Select(n =>
        {
            var isRead = readOnByNoticeId.TryGetValue(n.NoticeId, out var readOn);
            var isExpired = n.ExpiryDate is { } expiry && expiry < now;
            var isPinned = (n.Priority == NoticePriorities.Important || n.Priority == NoticePriorities.Urgent) && !isExpired && !isRead;

            return new NoticeResponse
            {
                NoticeId = n.NoticeId,
                Title = n.Title,
                Description = n.Description,
                CategoryId = n.CategoryId,
                CategoryName = categoryNames.GetValueOrDefault(n.CategoryId, string.Empty),
                Priority = n.Priority,
                Status = n.Status,
                PublishDate = n.PublishDate,
                ExpiryDate = n.ExpiryDate,
                CreatedByName = n.CreatedBy.HasValue ? createdByNames.GetValueOrDefault(n.CreatedBy.Value, string.Empty) : string.Empty,
                CreatedOn = n.CreatedOn,
                IsRead = isRead,
                ReadOn = isRead ? readOn : null,
                IsExpired = isExpired,
                IsPinned = isPinned,
                Attachments = attachmentsByNoticeId.GetValueOrDefault(n.NoticeId, new List<NoticeAttachmentResponse>()),
            };
        }).ToList();
    }

    private async Task<Dictionary<int, string>> ResolveDisplayNamesAsync(List<int> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var users = await (
            from u in db.UserLogins
            where userIds.Contains(u.UserId)
            join r in db.Residents on u.ResidentId equals r.ResidentId into residents
            from resident in residents.DefaultIfEmpty()
            select new { u.UserId, u.Username, ResidentFullName = resident != null ? resident.FullName : null }
        ).ToListAsync();

        return users.ToDictionary(
            u => u.UserId,
            u => !string.IsNullOrWhiteSpace(u.ResidentFullName) ? u.ResidentFullName! : u.Username);
    }
}
