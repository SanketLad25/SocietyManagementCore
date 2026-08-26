using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Upload/download for visitor photos — direct copy of ComplaintAttachmentService's shape:
/// local-disk storage, image-only, 20MB cap. Unlike ComplaintAttachment, VisitorAttachment has no
/// "kind" split (there's no separate resolution-photo concept here).
///
/// Files are organized under a per-society subfolder (sanitized SocietyName + "-" + SocietyId),
/// a deliberate deviation from every other attachment service in this app (Complaint/Notice use
/// one flat folder and rely purely on the SocietyId column for tenant isolation). SocietyName is
/// free-text with no DB uniqueness constraint, so it's sanitized (unsafe/path characters stripped)
/// and always suffixed with the numeric SocietyId — the suffix is what actually guarantees no two
/// societies can collide on the same folder, the sanitized name is only for human readability.
/// StorageRoot itself stays the flat "Content/Visitor" base; the subfolder is baked into
/// StoragePath on write, so existing rows created before this change (bare filename, no subfolder
/// prefix) keep resolving correctly under the same root with no migration needed.
public class VisitorAttachmentService(SocietyDbContext db, ICurrentUserContext currentUser, IWebHostEnvironment env, AuditLogService auditLog)
{
    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    private const long MaxFileSizeBytes = 20L * 1024 * 1024;

    private string StorageRoot => Path.Combine(env.ContentRootPath, "Content", "Visitor");

    private static readonly Regex UnsafeChars = new("[^a-z0-9]+", RegexOptions.Compiled);

    /// Sanitizes a society name into a safe, lowercase, hyphenated folder-name fragment. Always
    /// suffixed with the numeric SocietyId by the caller — that suffix, not this sanitization, is
    /// what actually prevents two societies from colliding on the same folder.
    private static string SanitizeFolderNameFragment(string? societyName)
    {
        var lowered = (societyName ?? string.Empty).ToLowerInvariant();
        var hyphenated = UnsafeChars.Replace(lowered, "-").Trim('-');
        if (hyphenated.Length > 60)
        {
            hyphenated = hyphenated[..60].Trim('-');
        }

        return string.IsNullOrEmpty(hyphenated) ? "society" : hyphenated;
    }

    private async Task<string> GetSocietySubfolderAsync(int societyId)
    {
        var societyName = await db.Societies
            .Where(s => s.SocietyId == societyId)
            .Select(s => s.SocietyName)
            .FirstOrDefaultAsync();

        return $"{SanitizeFolderNameFragment(societyName)}-{societyId}";
    }

    public async Task<ServiceResult<List<VisitorAttachmentResponse>>> UploadAsync(int visitorLogId, IReadOnlyList<IFormFile> files)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<List<VisitorAttachmentResponse>>.Fail("No society context for this account.");
        }

        var log = await db.VisitorLogs.FirstOrDefaultAsync(v => v.VisitorLogId == visitorLogId && v.SocietyId == societyId);
        if (log is null)
        {
            return ServiceResult<List<VisitorAttachmentResponse>>.Fail("Visitor entry not found.");
        }

        if (files.Count == 0)
        {
            return ServiceResult<List<VisitorAttachmentResponse>>.Fail("No files provided.");
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.ContainsKey(extension))
            {
                return ServiceResult<List<VisitorAttachmentResponse>>.Fail($"File type not allowed: {file.FileName}");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return ServiceResult<List<VisitorAttachmentResponse>>.Fail($"File too large (max 20 MB): {file.FileName}");
            }
        }

        var societySubfolder = await GetSocietySubfolderAsync(societyId);
        var societyDir = Path.Combine(StorageRoot, societySubfolder);
        Directory.CreateDirectory(societyDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var saved = new List<VisitorAttachment>();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var extension = Path.GetExtension(file.FileName);
            var safeFileName = Path.GetFileName(file.FileName);
            var storedFileName = $"{timestamp}_{i}_{safeFileName}";
            var relativePath = Path.Combine(societySubfolder, storedFileName);
            var fullPath = Path.Combine(StorageRoot, relativePath);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new VisitorAttachment
            {
                SocietyId = societyId,
                VisitorLogId = visitorLogId,
                FileName = safeFileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? AllowedExtensions[extension] : file.ContentType,
                FileSizeBytes = file.Length,
                StoragePath = relativePath,
                UploadedOn = DateTime.UtcNow,
                UploadedBy = currentUser.UserId,
            };

            db.VisitorAttachments.Add(attachment);
            saved.Add(attachment);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<List<VisitorAttachmentResponse>>.Fail("Unable to save photo(s). Please try again.");
        }

        await auditLog.LogAsync("UploadPhotos", "VisitorLog", visitorLogId, $"{files.Count} file(s)");

        return ServiceResult<List<VisitorAttachmentResponse>>.Ok(saved.Select(ToResponse).ToList());
    }

    /// Service-scoped read — matches VisitorLogService.GetAsync's role-scoping (Resident -> own
    /// flat only; Security/Committee -> society-wide), returned as not-found rather than forbidden
    /// per this app's no-enumeration convention.
    public async Task<ServiceResult<(string PhysicalPath, string ContentType, string FileName)>> GetDownloadInfoAsync(int visitorLogId, int attachmentId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<(string, string, string)>.Fail("No society context for this account.");
        }

        var log = await db.VisitorLogs.FirstOrDefaultAsync(v => v.VisitorLogId == visitorLogId && v.SocietyId == societyId);
        if (log is null)
        {
            return ServiceResult<(string, string, string)>.Fail("Visitor entry not found.");
        }

        if (currentUser.RoleName == RoleNames.Resident)
        {
            var myFlatId = await db.UserLogins
                .Where(u => u.UserId == currentUser.UserId)
                .Join(db.Residents, u => u.ResidentId, r => r.ResidentId, (u, r) => r.FlatId)
                .FirstOrDefaultAsync();
            if (myFlatId is null || log.FlatId != myFlatId)
            {
                return ServiceResult<(string, string, string)>.Fail("Visitor entry not found.");
            }
        }

        var attachment = await db.VisitorAttachments.FirstOrDefaultAsync(a =>
            a.VisitorAttachmentId == attachmentId && a.VisitorLogId == visitorLogId && a.SocietyId == societyId);
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

    /// Mirrors NoticeAttachmentService.DeletePhysicalFile's exact shape; called by
    /// VisitorLogService.DeleteAsync for each attachment before the VisitorAttachment rows
    /// themselves are removed.
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

    private static VisitorAttachmentResponse ToResponse(VisitorAttachment attachment) => new()
    {
        VisitorAttachmentId = attachment.VisitorAttachmentId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSizeBytes = attachment.FileSizeBytes,
        UploadedOn = attachment.UploadedOn,
    };
}
