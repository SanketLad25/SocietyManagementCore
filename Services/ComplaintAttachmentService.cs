using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public static class ComplaintAttachmentKinds
{
    public const string Complaint = "Complaint";
    public const string Resolution = "Resolution";
}

/// Upload/download for complaint photos (complaints.md §3) — same flat-storage pattern as
/// NoticeAttachmentService, narrowed to images only. "Complaint"-kind photos come from the owning
/// resident at submission time; "Resolution"-kind photos come from Admin once work is underway.
public class ComplaintAttachmentService(SocietyDbContext db, ICurrentUserContext currentUser, IWebHostEnvironment env, AuditLogService auditLog)
{
    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    private const long MaxFileSizeBytes = 20L * 1024 * 1024;

    private string StorageRoot => Path.Combine(env.ContentRootPath, "Content", "Complaint");

    public async Task<ServiceResult<List<ComplaintAttachmentResponse>>> UploadAsync(int complaintId, string kind, IReadOnlyList<IFormFile> files)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("No society context for this account.");
        }

        if (kind != ComplaintAttachmentKinds.Complaint && kind != ComplaintAttachmentKinds.Resolution)
        {
            return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("Invalid attachment kind.");
        }

        var complaint = await db.Complaints.FirstOrDefaultAsync(c => c.ComplaintId == complaintId && c.SocietyId == societyId);
        if (complaint is null)
        {
            return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("Complaint not found.");
        }

        if (kind == ComplaintAttachmentKinds.Complaint)
        {
            if (currentUser.ResidentId != complaint.ResidentId)
            {
                return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("Only the resident who raised this complaint can add photos to it.");
            }

            if (complaint.Status == ComplaintStatuses.Closed)
            {
                return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("This complaint is closed.");
            }
        }
        else
        {
            if (currentUser.RoleName != RoleNames.Admin)
            {
                return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("Only Admin can add resolution photos.");
            }

            if (complaint.Status is not (ComplaintStatuses.Assigned or ComplaintStatuses.InProgress or ComplaintStatuses.Resolved))
            {
                return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("Resolution photos can only be added once the complaint is assigned.");
            }
        }

        if (files.Count == 0)
        {
            return ServiceResult<List<ComplaintAttachmentResponse>>.Fail("No files provided.");
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.ContainsKey(extension))
            {
                return ServiceResult<List<ComplaintAttachmentResponse>>.Fail($"File type not allowed: {file.FileName}");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return ServiceResult<List<ComplaintAttachmentResponse>>.Fail($"File too large (max 20 MB): {file.FileName}");
            }
        }

        Directory.CreateDirectory(StorageRoot);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var saved = new List<ComplaintAttachment>();

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

            var attachment = new ComplaintAttachment
            {
                SocietyId = societyId,
                ComplaintId = complaintId,
                AttachmentKind = kind,
                FileName = safeFileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? AllowedExtensions[extension] : file.ContentType,
                FileSizeBytes = file.Length,
                StoragePath = storedFileName,
                UploadedOn = DateTime.UtcNow,
                UploadedBy = currentUser.UserId,
            };

            db.ComplaintAttachments.Add(attachment);
            saved.Add(attachment);
        }

        await db.SaveChangesAsync();
        await auditLog.LogAsync($"Upload{kind}Photos", "Complaint", complaintId, $"{files.Count} file(s)");

        return ServiceResult<List<ComplaintAttachmentResponse>>.Ok(saved.Select(ToResponse).ToList());
    }

    public async Task<ServiceResult<(string PhysicalPath, string ContentType, string FileName)>> GetDownloadInfoAsync(int complaintId, int attachmentId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<(string, string, string)>.Fail("No society context for this account.");
        }

        var attachment = await db.ComplaintAttachments.FirstOrDefaultAsync(a =>
            a.ComplaintAttachmentId == attachmentId && a.ComplaintId == complaintId && a.SocietyId == societyId);
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

    private static ComplaintAttachmentResponse ToResponse(ComplaintAttachment attachment) => new()
    {
        ComplaintAttachmentId = attachment.ComplaintAttachmentId,
        AttachmentKind = attachment.AttachmentKind,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSizeBytes = attachment.FileSizeBytes,
        UploadedOn = attachment.UploadedOn,
    };
}
