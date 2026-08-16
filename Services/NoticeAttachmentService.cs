using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Upload/download/delete for notice attachments (notice.md §3). Files land flat under
/// {ContentRoot}/Content/Notice, named "{yyyyMMddHHmmssfff}_{batchIndex}_{originalFileName}" — never
/// served as static content, always mediated through the auth-checked controller actions.
public class NoticeAttachmentService(SocietyDbContext db, ICurrentUserContext currentUser, IWebHostEnvironment env, AuditLogService auditLog)
{
    private static readonly string[] ManagerRoles = RoleNames.NoticeManagerRoles.Split(',');

    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    private const long MaxFileSizeBytes = 20L * 1024 * 1024;

    private string StorageRoot => Path.Combine(env.ContentRootPath, "Content", "Notice");

    public async Task<ServiceResult<List<NoticeAttachmentResponse>>> UploadAsync(int noticeId, IReadOnlyList<IFormFile> files)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<List<NoticeAttachmentResponse>>.Fail("No society context for this account.");
        }

        var notice = await db.Notices.FirstOrDefaultAsync(n => n.NoticeId == noticeId && n.SocietyId == societyId);
        if (notice is null)
        {
            return ServiceResult<List<NoticeAttachmentResponse>>.Fail("Notice not found.");
        }

        if (notice.Status == NoticeStatuses.Published)
        {
            return ServiceResult<List<NoticeAttachmentResponse>>.Fail("Cannot add attachments to a published notice.");
        }

        if (files.Count == 0)
        {
            return ServiceResult<List<NoticeAttachmentResponse>>.Fail("No files provided.");
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.ContainsKey(extension))
            {
                return ServiceResult<List<NoticeAttachmentResponse>>.Fail($"File type not allowed: {file.FileName}");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return ServiceResult<List<NoticeAttachmentResponse>>.Fail($"File too large (max 20 MB): {file.FileName}");
            }
        }

        Directory.CreateDirectory(StorageRoot);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var saved = new List<NoticeAttachment>();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var extension = Path.GetExtension(file.FileName);
            var safeFileName = Path.GetFileName(file.FileName);
            var storedFileName = $"{timestamp}_{i}_{safeFileName}";
            var fullPath = Path.Combine(StorageRoot, storedFileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new NoticeAttachment
            {
                SocietyId = societyId,
                NoticeId = noticeId,
                FileName = safeFileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? AllowedExtensions[extension] : file.ContentType,
                FileSizeBytes = file.Length,
                StoragePath = storedFileName,
                UploadedOn = DateTime.UtcNow,
                UploadedBy = currentUser.UserId,
            };

            db.NoticeAttachments.Add(attachment);
            saved.Add(attachment);
        }

        await db.SaveChangesAsync();
        await auditLog.LogAsync("UploadAttachments", "Notice", noticeId, $"{files.Count} file(s)");

        return ServiceResult<List<NoticeAttachmentResponse>>.Ok(saved.Select(ToResponse).ToList());
    }

    public async Task<ServiceResult<(string PhysicalPath, string ContentType, string FileName)>> GetDownloadInfoAsync(int noticeId, int attachmentId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<(string, string, string)>.Fail("No society context for this account.");
        }

        var notice = await db.Notices.FirstOrDefaultAsync(n => n.NoticeId == noticeId && n.SocietyId == societyId);
        if (notice is null || (notice.Status != NoticeStatuses.Published && !IsManager()))
        {
            return ServiceResult<(string, string, string)>.Fail("Attachment not found.");
        }

        var attachment = await db.NoticeAttachments.FirstOrDefaultAsync(a => a.NoticeAttachmentId == attachmentId && a.NoticeId == noticeId);
        if (attachment is null)
        {
            return ServiceResult<(string, string, string)>.Fail("Attachment not found.");
        }

        var fullPath = Path.Combine(StorageRoot, attachment.StoragePath);
        if (!File.Exists(fullPath))
        {
            return ServiceResult<(string, string, string)>.Fail("Attachment file is missing.");
        }

        return ServiceResult<(string, string, string)>.Ok((fullPath, attachment.ContentType, attachment.FileName));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int noticeId, int attachmentId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<bool>.Fail("No society context for this account.");
        }

        var notice = await db.Notices.FirstOrDefaultAsync(n => n.NoticeId == noticeId && n.SocietyId == societyId);
        if (notice is null)
        {
            return ServiceResult<bool>.Fail("Notice not found.");
        }

        if (notice.Status == NoticeStatuses.Published)
        {
            return ServiceResult<bool>.Fail("Cannot remove attachments from a published notice.");
        }

        var attachment = await db.NoticeAttachments.FirstOrDefaultAsync(a => a.NoticeAttachmentId == attachmentId && a.NoticeId == noticeId);
        if (attachment is null)
        {
            return ServiceResult<bool>.Fail("Attachment not found.");
        }

        DeletePhysicalFile(attachment.StoragePath);
        db.NoticeAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("DeleteAttachment", "Notice", noticeId, attachment.FileName);

        return ServiceResult<bool>.Ok(true);
    }

    public void DeletePhysicalFile(string storagePath)
    {
        try
        {
            var fullPath = Path.Combine(StorageRoot, storagePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Best-effort — an orphaned file on disk isn't worth failing the whole delete over.
        }
    }

    private bool IsManager() => currentUser.RoleName is { } role && ManagerRoles.Contains(role);

    private static NoticeAttachmentResponse ToResponse(NoticeAttachment attachment) => new()
    {
        NoticeAttachmentId = attachment.NoticeAttachmentId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSizeBytes = attachment.FileSizeBytes,
        UploadedOn = attachment.UploadedOn,
    };
}
