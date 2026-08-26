using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// The core Visitor Management service. Role-scoped read (Resident -> own flat only; Security/
/// Committee -> society-wide, VisitorManagement.md "Services" — the opposite of Complaints/
/// Events' broad-visibility exception, visitors are flat-private for residents). Status pipeline
/// transitions go through VisitorLogStatuses.CanTransition's explicit allowed-next-states map
/// rather than a linear order array, since the pipeline branches.
public class VisitorLogService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    AuditLogService auditLog,
    VisitorNotificationService notificationService,
    VisitorAttachmentService attachmentService)
{
    public async Task<List<VisitorLogResponse>> ListAsync(string? status, int? flatId, string? search)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<VisitorLogResponse>();
        }

        var query = db.VisitorLogs.Where(v => v.SocietyId == societyId);

        if (currentUser.RoleName == RoleNames.Resident)
        {
            var myFlatId = await ResolveMyFlatIdAsync();
            if (myFlatId is not { } resolvedFlatId)
            {
                return new List<VisitorLogResponse>();
            }

            query = query.Where(v => v.FlatId == resolvedFlatId);
        }
        else if (flatId is { } requestedFlatId)
        {
            query = query.Where(v => v.FlatId == requestedFlatId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(v => v.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                v.PrimaryVisitorName.Contains(term) ||
                (v.PrimaryMobile != null && v.PrimaryMobile.Contains(term)) ||
                db.Flats.Any(f => f.FlatId == v.FlatId && f.FlatNo != null && f.FlatNo.Contains(term)));
        }

        var logs = await query.OrderByDescending(v => v.CreatedOn).ToListAsync();
        return await ToResponsesAsync(logs);
    }

    /// Not-found (not forbidden) for a flat that isn't the caller's — matches ComplaintService's
    /// no-enumeration convention.
    public async Task<VisitorLogResponse?> GetAsync(int visitorLogId)
    {
        var log = await FindInSocietyAsync(visitorLogId);
        if (log is null)
        {
            return null;
        }

        if (currentUser.RoleName == RoleNames.Resident)
        {
            var myFlatId = await ResolveMyFlatIdAsync();
            if (myFlatId is null || log.FlatId != myFlatId)
            {
                return null;
            }
        }

        var responses = await ToResponsesAsync(new List<VisitorLog> { log });
        return responses.FirstOrDefault();
    }

    /// VisitorGateRoles only (enforced by the controller too). Copies ApprovalRequired from the
    /// category at creation time — no per-visit guard override (resolved decision #4). No-approval
    /// categories auto-check-in immediately (resolved decision #1): one guard action, not two.
    public async Task<ServiceResult<VisitorLogResponse>> CreateAsync(VisitorLogRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<VisitorLogResponse>.Fail("No society context for this account.");
        }

        var category = await db.VisitorCategories.FirstOrDefaultAsync(c =>
            c.VisitorCategoryId == request.VisitorCategoryId && c.SocietyId == societyId && c.IsActive);
        if (category is null)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Invalid category.");
        }

        var flatExists = await db.Flats.AnyAsync(f => f.FlatId == request.FlatId && f.SocietyId == societyId);
        if (!flatExists)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Invalid flat.");
        }

        if (category.RequiresVehicleNo && string.IsNullOrWhiteSpace(request.VehicleNo))
        {
            return ServiceResult<VisitorLogResponse>.Fail($"Vehicle number is required for {category.CategoryName} visitors.");
        }

        if (category.RequiresCompanyName && string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return ServiceResult<VisitorLogResponse>.Fail($"Company name is required for {category.CategoryName} visitors.");
        }

        var mobile = string.IsNullOrWhiteSpace(request.PrimaryMobile) ? null : request.PrimaryMobile.Trim();
        if (mobile is not null)
        {
            var hasOpenEntry = await db.VisitorLogs.AnyAsync(v =>
                v.SocietyId == societyId && v.FlatId == request.FlatId && v.PrimaryMobile == mobile &&
                VisitorLogStatuses.NonTerminal.Contains(v.Status));
            if (hasOpenEntry)
            {
                return ServiceResult<VisitorLogResponse>.Fail("This visitor already has an open entry for this flat. Check them out or cancel it first.");
            }
        }

        var approvalRequired = category.RequiresApprovalDefault;
        var now = DateTime.UtcNow;

        var log = new VisitorLog
        {
            SocietyId = societyId,
            FlatId = request.FlatId,
            VisitorCategoryId = request.VisitorCategoryId,
            PrimaryVisitorName = request.PrimaryVisitorName.Trim(),
            PrimaryMobile = mobile,
            VehicleNo = string.IsNullOrWhiteSpace(request.VehicleNo) ? null : request.VehicleNo.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim(),
            PartySize = request.PartySize is > 0 ? request.PartySize.Value : 1,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(),
            ApprovalRequired = approvalRequired,
            Status = approvalRequired ? VisitorLogStatuses.PendingApproval : VisitorLogStatuses.CheckedIn,
            EntryTime = approvalRequired ? null : now,
            LoggedByUserId = currentUser.UserId,
        };

        db.VisitorLogs.Add(log);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Unable to save visitor entry. Please try again.");
        }

        var memberNames = request.MemberNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
        if (memberNames is { Count: > 0 })
        {
            foreach (var name in memberNames)
            {
                db.VisitorLogMembers.Add(new VisitorLogMember
                {
                    SocietyId = societyId,
                    VisitorLogId = log.VisitorLogId,
                    MemberName = name,
                    CreatedOn = now,
                });
            }

            await db.SaveChangesAsync();
        }

        await auditLog.LogAsync("Log", "VisitorLog", log.VisitorLogId, log.PrimaryVisitorName);

        if (approvalRequired)
        {
            await notificationService.NotifyFlatResidentsAsync(
                log,
                $"New visitor {log.PrimaryVisitorName} ({category.CategoryName}) is awaiting your approval.");
        }

        var responses = await ToResponsesAsync(new List<VisitorLog> { log });
        return ServiceResult<VisitorLogResponse>.Ok(responses[0]);
    }

    /// Resident of the visited flat, or CommitteeRoles (resolved decision #2 — a deliberate
    /// access-control widening with no other precedent in this app).
    public Task<ServiceResult<VisitorLogResponse>> ApproveAsync(int visitorLogId) => DecideAsync(visitorLogId, approve: true);

    public Task<ServiceResult<VisitorLogResponse>> RejectAsync(int visitorLogId) => DecideAsync(visitorLogId, approve: false);

    private async Task<ServiceResult<VisitorLogResponse>> DecideAsync(int visitorLogId, bool approve)
    {
        var log = await FindInSocietyAsync(visitorLogId);
        if (log is null)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Visitor entry not found.");
        }

        // Authorization must be checked before the status-transition check below — otherwise a
        // resident of a different flat can probe a VisitorLogId belonging to someone else's flat
        // and learn its actual status from the transition-failure message before ever being told
        // they're not authorized, matching ComplaintService.ConfirmResolutionAsync's ordering.
        if (!await CanActOnFlatAsync(log.FlatId))
        {
            return ServiceResult<VisitorLogResponse>.Fail("You are not authorized to decide on this visitor.");
        }

        var targetStatus = approve ? VisitorLogStatuses.Approved : VisitorLogStatuses.Rejected;
        if (!VisitorLogStatuses.CanTransition(log.Status, targetStatus))
        {
            return ServiceResult<VisitorLogResponse>.Fail($"Cannot {(approve ? "approve" : "reject")} — this entry is {log.Status}, not awaiting approval.");
        }

        log.Status = targetStatus;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Unable to update visitor entry. Please try again.");
        }

        await auditLog.LogAsync(approve ? "Approve" : "Reject", "VisitorLog", log.VisitorLogId, null);

        if (log.LoggedByUserId is { } loggedByUserId)
        {
            await notificationService.NotifyUserAsync(
                log, loggedByUserId, $"Visitor {log.PrimaryVisitorName} was {(approve ? "approved" : "rejected")}.");
        }

        var responses = await ToResponsesAsync(new List<VisitorLog> { log });
        return ServiceResult<VisitorLogResponse>.Ok(responses[0]);
    }

    /// VisitorGateRoles only. Only reachable from Approved — no-approval categories are already
    /// CheckedIn at creation, so there's never a separate check-in click for them.
    public async Task<ServiceResult<VisitorLogResponse>> CheckInAsync(int visitorLogId)
    {
        var log = await FindInSocietyAsync(visitorLogId);
        if (log is null)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Visitor entry not found.");
        }

        if (!VisitorLogStatuses.CanTransition(log.Status, VisitorLogStatuses.CheckedIn))
        {
            return ServiceResult<VisitorLogResponse>.Fail(
                log.Status == VisitorLogStatuses.PendingApproval
                    ? "This visitor is still awaiting approval."
                    : $"Cannot check in — this entry is {log.Status}.");
        }

        log.Status = VisitorLogStatuses.CheckedIn;
        log.EntryTime = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Unable to update visitor entry. Please try again.");
        }

        await auditLog.LogAsync("CheckIn", "VisitorLog", log.VisitorLogId, null);

        var responses = await ToResponsesAsync(new List<VisitorLog> { log });
        return ServiceResult<VisitorLogResponse>.Ok(responses[0]);
    }

    public async Task<ServiceResult<VisitorLogResponse>> CheckOutAsync(int visitorLogId)
    {
        var log = await FindInSocietyAsync(visitorLogId);
        if (log is null)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Visitor entry not found.");
        }

        if (!VisitorLogStatuses.CanTransition(log.Status, VisitorLogStatuses.CheckedOut))
        {
            return ServiceResult<VisitorLogResponse>.Fail($"Cannot check out — this entry is {log.Status}.");
        }

        log.Status = VisitorLogStatuses.CheckedOut;
        log.ExitTime = DateTime.UtcNow;
        log.CheckedOutByUserId = currentUser.UserId;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<VisitorLogResponse>.Fail("Unable to update visitor entry. Please try again.");
        }

        await auditLog.LogAsync("CheckOut", "VisitorLog", log.VisitorLogId, null);

        var responses = await ToResponsesAsync(new List<VisitorLog> { log });
        return ServiceResult<VisitorLogResponse>.Ok(responses[0]);
    }


    /// Hard delete — VisitorGateRoles only (enforced by the controller), any status. A deliberate
    /// choice (not the soft-delete/archive alternative) following NoticeService.DeleteAsync's
    /// exact shape — the closest existing precedent in this app for removing a content record
    /// along with its attachments. Removes the VisitorLog row plus every VisitorLogMember/
    /// VisitorAttachment/VisitorNotification row tied to it, and each attachment's physical file.
    public async Task<ServiceResult<bool>> DeleteAsync(int visitorLogId)
    {
        var log = await FindInSocietyAsync(visitorLogId);
        if (log is null)
        {
            return ServiceResult<bool>.Fail("Visitor entry not found.");
        }

        var attachments = await db.VisitorAttachments.Where(a => a.VisitorLogId == visitorLogId).ToListAsync();
        foreach (var attachment in attachments)
        {
            attachmentService.DeletePhysicalFile(attachment.StoragePath);
        }
        db.VisitorAttachments.RemoveRange(attachments);

        var members = await db.VisitorLogMembers.Where(m => m.VisitorLogId == visitorLogId).ToListAsync();
        db.VisitorLogMembers.RemoveRange(members);

        var notifications = await db.VisitorNotifications.Where(n => n.VisitorLogId == visitorLogId).ToListAsync();
        db.VisitorNotifications.RemoveRange(notifications);

        db.VisitorLogs.Remove(log);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<bool>.Fail("Unable to delete visitor entry. Please try again.");
        }

        await auditLog.LogAsync("Delete", "VisitorLog", visitorLogId, log.PrimaryVisitorName);

        return ServiceResult<bool>.Ok(true);
    }

    private Task<VisitorLog?> FindInSocietyAsync(int visitorLogId) =>
        currentUser.SocietyId is { } societyId
            ? db.VisitorLogs.FirstOrDefaultAsync(v => v.VisitorLogId == visitorLogId && v.SocietyId == societyId)
            : Task.FromResult<VisitorLog?>(null);

    private async Task<int?> ResolveMyFlatIdAsync()
    {
        if (currentUser.ResidentId is not { } residentId)
        {
            return null;
        }

        return await db.Residents.Where(r => r.ResidentId == residentId).Select(r => r.FlatId).FirstOrDefaultAsync();
    }

    private static bool IsCommitteeRole(string? roleName) =>
        roleName is RoleNames.Admin or RoleNames.Chairman or RoleNames.Secretary or RoleNames.Treasurer;

    /// Resident of `flatId`, or any CommitteeRoles member (resolved decision #2).
    private async Task<bool> CanActOnFlatAsync(int flatId)
    {
        if (IsCommitteeRole(currentUser.RoleName))
        {
            return true;
        }

        var myFlatId = await ResolveMyFlatIdAsync();
        return myFlatId is not null && myFlatId == flatId;
    }

    private async Task<List<VisitorLogResponse>> ToResponsesAsync(List<VisitorLog> logs)
    {
        if (logs.Count == 0)
        {
            return new List<VisitorLogResponse>();
        }

        var flatIds = logs.Select(l => l.FlatId).Distinct().ToList();
        var flatNos = await db.Flats.Where(f => flatIds.Contains(f.FlatId)).ToDictionaryAsync(f => f.FlatId, f => f.FlatNo ?? string.Empty);

        var categoryIds = logs.Select(l => l.VisitorCategoryId).Distinct().ToList();
        var categoryNames = await db.VisitorCategories
            .Where(c => categoryIds.Contains(c.VisitorCategoryId))
            .ToDictionaryAsync(c => c.VisitorCategoryId, c => c.CategoryName);

        var userIds = logs.SelectMany(l => new[] { l.LoggedByUserId, l.CheckedOutByUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var displayNames = await ResolveDisplayNamesAsync(userIds);

        var logIds = logs.Select(l => l.VisitorLogId).ToList();

        var membersByLogId = await db.VisitorLogMembers
            .Where(m => logIds.Contains(m.VisitorLogId))
            .GroupBy(m => m.VisitorLogId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(m => m.MemberName).ToList());

        var attachmentsByLogId = await db.VisitorAttachments
            .Where(a => logIds.Contains(a.VisitorLogId))
            .GroupBy(a => a.VisitorLogId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(a => new VisitorAttachmentResponse
                {
                    VisitorAttachmentId = a.VisitorAttachmentId,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSizeBytes = a.FileSizeBytes,
                    UploadedOn = a.UploadedOn,
                }).ToList());

        var isCommittee = IsCommitteeRole(currentUser.RoleName);
        var isGate = currentUser.RoleName is RoleNames.Admin or RoleNames.Security;
        int? myFlatId = null;
        if (currentUser.RoleName == RoleNames.Resident)
        {
            myFlatId = await ResolveMyFlatIdAsync();
        }

        return logs.Select(log =>
        {
            var canDecide = isCommittee || (myFlatId is not null && myFlatId == log.FlatId);

            return new VisitorLogResponse
            {
                VisitorLogId = log.VisitorLogId,
                FlatId = log.FlatId,
                FlatNo = flatNos.GetValueOrDefault(log.FlatId, string.Empty),
                VisitorCategoryId = log.VisitorCategoryId,
                CategoryName = categoryNames.GetValueOrDefault(log.VisitorCategoryId, string.Empty),
                PrimaryVisitorName = log.PrimaryVisitorName,
                PrimaryMobile = log.PrimaryMobile,
                VehicleNo = log.VehicleNo,
                CompanyName = log.CompanyName,
                PartySize = log.PartySize,
                Purpose = log.Purpose,
                ApprovalRequired = log.ApprovalRequired,
                Status = log.Status,
                EntryTime = log.EntryTime,
                ExitTime = log.ExitTime,
                LoggedByName = log.LoggedByUserId.HasValue ? displayNames.GetValueOrDefault(log.LoggedByUserId.Value, string.Empty) : string.Empty,
                CheckedOutByName = log.CheckedOutByUserId.HasValue ? displayNames.GetValueOrDefault(log.CheckedOutByUserId.Value) : null,
                CreatedOn = log.CreatedOn,
                MemberNames = membersByLogId.GetValueOrDefault(log.VisitorLogId, new List<string>()),
                Attachments = attachmentsByLogId.GetValueOrDefault(log.VisitorLogId, new List<VisitorAttachmentResponse>()),
                CanApprove = log.Status == VisitorLogStatuses.PendingApproval && canDecide,
                CanReject = log.Status == VisitorLogStatuses.PendingApproval && canDecide,
                CanCheckIn = log.Status == VisitorLogStatuses.Approved && isGate,
                CanCheckOut = log.Status == VisitorLogStatuses.CheckedIn && isGate,
                CanDelete = isGate,
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
