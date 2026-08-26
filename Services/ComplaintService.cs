using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class ComplaintService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    AuditLogService auditLog,
    ComplaintNotificationService notificationService)
{
    /// Any authenticated society member sees every complaint (complaints.md §0/§8) — visibility is
    /// universal, only writing is narrow.
    public async Task<List<ComplaintResponse>> ListAsync(string? status, int? categoryId, string? priority, string? search)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<ComplaintResponse>();
        }

        var query = db.Complaints.Where(c => c.SocietyId == societyId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (categoryId is { } cid)
        {
            query = query.Where(c => c.CategoryId == cid);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(c => c.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Description.Contains(term));
        }

        var complaints = await query.OrderByDescending(c => c.CreatedOn).ToListAsync();
        return await ToResponsesAsync(complaints);
    }

    /// Any authenticated society member (matches universal view access, complaints.md §8).
    public async Task<ComplaintStatsResponse> GetStatsAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new ComplaintStatsResponse();
        }

        var complaints = await db.Complaints
            .Where(c => c.SocietyId == societyId)
            .Select(c => new { c.Status, c.CategoryId, c.Priority })
            .ToListAsync();

        var categoryNames = await db.ComplaintCategories
            .Where(c => c.SocietyId == societyId)
            .ToDictionaryAsync(c => c.ComplaintCategoryId, c => c.CategoryName);

        var byStatus = ComplaintStatuses.All.ToDictionary(s => s, s => complaints.Count(c => c.Status == s));
        var byPriority = ComplaintPriorities.All.ToDictionary(p => p, p => complaints.Count(c => c.Priority == p));
        var byCategory = complaints
            .GroupBy(c => c.CategoryId)
            .ToDictionary(g => categoryNames.GetValueOrDefault(g.Key, "Unknown"), g => g.Count());

        var activeCount = complaints.Count(c =>
            c.Status is ComplaintStatuses.Open or ComplaintStatuses.Assigned or ComplaintStatuses.InProgress);

        return new ComplaintStatsResponse
        {
            TotalCount = complaints.Count,
            ActiveCount = activeCount,
            ByStatus = byStatus,
            ByCategory = byCategory,
            ByPriority = byPriority,
        };
    }

    public async Task<ComplaintResponse?> GetAsync(int complaintId)
    {
        var complaint = await FindInSocietyAsync(complaintId);
        if (complaint is null)
        {
            return null;
        }

        var responses = await ToResponsesAsync(new List<Complaint> { complaint });
        return responses.FirstOrDefault();
    }

    /// Resident only. Resolves the caller's own ResidentId/current FlatId — a resident always
    /// raises a complaint as themselves, never on behalf of someone else.
    ///
    /// `idempotencyKey` (SocChatBot.md Phase 5): null for every ordinary REST call — only
    /// ChatOrchestrationService.ConfirmActionAsync passes a real one, when raise_complaint is
    /// executed after the resident explicitly confirms it via the chatbot. A retried confirm with the
    /// same token must not create a second complaint, so an existing row with the same
    /// (SocietyId, IdempotencyKey) is returned as-is instead of inserting again.
    public async Task<ServiceResult<ComplaintResponse>> CreateAsync(ComplaintRequest request, Guid? idempotencyKey = null)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<ComplaintResponse>.Fail("No society context for this account.");
        }

        if (currentUser.ResidentId is not { } residentId)
        {
            return ServiceResult<ComplaintResponse>.Fail("Only a resident account can raise a complaint.");
        }

        if (!ComplaintPriorities.All.Contains(request.Priority))
        {
            return ServiceResult<ComplaintResponse>.Fail("Invalid priority.");
        }

        if (idempotencyKey is { } key)
        {
            var existing = await db.Complaints.FirstOrDefaultAsync(c => c.SocietyId == societyId && c.IdempotencyKey == key);
            if (existing is not null)
            {
                var existingResponses = await ToResponsesAsync(new List<Complaint> { existing });
                return ServiceResult<ComplaintResponse>.Ok(existingResponses[0]);
            }
        }

        var categoryValid = await db.ComplaintCategories.AnyAsync(c =>
            c.SocietyId == societyId && c.ComplaintCategoryId == request.CategoryId && c.IsActive);
        if (!categoryValid)
        {
            return ServiceResult<ComplaintResponse>.Fail("Invalid category.");
        }

        var resident = await db.Residents.FirstOrDefaultAsync(r => r.ResidentId == residentId);

        var complaint = new Complaint
        {
            SocietyId = societyId,
            ResidentId = residentId,
            FlatId = resident?.FlatId,
            CategoryId = request.CategoryId,
            Description = request.Description,
            Priority = request.Priority,
            Status = ComplaintStatuses.Open,
            IdempotencyKey = idempotencyKey,
        };

        db.Complaints.Add(complaint);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Covers a raced retry on the same idempotencyKey slipping past the lookup above (two
            // near-simultaneous confirms) — UX_Complaint_IdempotencyKey rejects the second insert;
            // surface it as a friendly failure instead of a raw 500, matching every other service's
            // insert/delete convention in this app.
            return ServiceResult<ComplaintResponse>.Fail("Unable to save complaint. Please try again.");
        }

        db.ComplaintUpdates.Add(new ComplaintUpdate
        {
            SocietyId = societyId,
            ComplaintId = complaint.ComplaintId,
            UpdateType = "StatusChange",
            OldStatus = null,
            NewStatus = ComplaintStatuses.Open,
            CreatedBy = currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await notificationService.NotifyManagersAsync(complaint);

        var responses = await ToResponsesAsync(new List<Complaint> { complaint });
        return ServiceResult<ComplaintResponse>.Ok(responses[0]);
    }

    /// Admin only (complaints.md §1/§8 — reversed from the first draft's Admin/Secretary/Chairman
    /// split). Records who's doing the work and flips Open → Assigned as a side effect.
    public async Task<ServiceResult<ComplaintResponse>> AssignAsync(int complaintId, ComplaintAssignRequest request)
    {
        var complaint = await FindInSocietyAsync(complaintId);
        if (complaint is null)
        {
            return ServiceResult<ComplaintResponse>.Fail("Complaint not found.");
        }

        if (complaint.Status is ComplaintStatuses.Resolved or ComplaintStatuses.Closed)
        {
            return ServiceResult<ComplaintResponse>.Fail("Cannot assign a complaint that is already resolved or closed.");
        }

        complaint.AssignedToName = request.AssignedToName;
        complaint.AssignedToContact = request.AssignedToContact;
        complaint.AssignmentNotes = request.AssignmentNotes;
        complaint.AssignedBy = currentUser.UserId;
        complaint.AssignedOn = DateTime.UtcNow;

        var oldStatus = complaint.Status;
        if (complaint.Status == ComplaintStatuses.Open)
        {
            complaint.Status = ComplaintStatuses.Assigned;
        }

        await db.SaveChangesAsync();

        db.ComplaintUpdates.Add(new ComplaintUpdate
        {
            SocietyId = complaint.SocietyId,
            ComplaintId = complaint.ComplaintId,
            UpdateType = "Assignment",
            OldStatus = oldStatus == complaint.Status ? null : oldStatus,
            NewStatus = oldStatus == complaint.Status ? null : complaint.Status,
            CommentText = $"Assigned to {request.AssignedToName}" + (string.IsNullOrWhiteSpace(request.AssignmentNotes) ? "" : $" — {request.AssignmentNotes}"),
            CreatedBy = currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Assign", "Complaint", complaint.ComplaintId, request.AssignedToName);

        var responses = await ToResponsesAsync(new List<Complaint> { complaint });
        return ServiceResult<ComplaintResponse>.Ok(responses[0]);
    }

    /// Admin only. Only the next step in ComplaintStatuses.Order is accepted — no skipping, no
    /// going backward (complaints.md §4/§7). Notifies the resident on every change.
    public async Task<ServiceResult<ComplaintResponse>> UpdateStatusAsync(int complaintId, ComplaintStatusUpdateRequest request)
    {
        var complaint = await FindInSocietyAsync(complaintId);
        if (complaint is null)
        {
            return ServiceResult<ComplaintResponse>.Fail("Complaint not found.");
        }

        if (request.NewStatus == ComplaintStatuses.Assigned)
        {
            return ServiceResult<ComplaintResponse>.Fail("Use the Assign action to move a complaint to Assigned.");
        }

        var currentIndex = Array.IndexOf(ComplaintStatuses.Order, complaint.Status);
        var nextIndex = Array.IndexOf(ComplaintStatuses.Order, request.NewStatus);
        if (nextIndex != currentIndex + 1)
        {
            return ServiceResult<ComplaintResponse>.Fail(
                $"Cannot move from {complaint.Status} to {request.NewStatus} — only the next step ({(currentIndex + 1 < ComplaintStatuses.Order.Length ? ComplaintStatuses.Order[currentIndex + 1] : "none")}) is allowed.");
        }

        var oldStatus = complaint.Status;
        complaint.Status = request.NewStatus;
        if (request.NewStatus == ComplaintStatuses.Closed)
        {
            complaint.ClosedOn = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        db.ComplaintUpdates.Add(new ComplaintUpdate
        {
            SocietyId = complaint.SocietyId,
            ComplaintId = complaint.ComplaintId,
            UpdateType = "StatusChange",
            OldStatus = oldStatus,
            NewStatus = request.NewStatus,
            CommentText = request.Comment,
            CreatedBy = currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await auditLog.LogAsync("UpdateStatus", "Complaint", complaint.ComplaintId, $"{oldStatus} -> {request.NewStatus}");

        await notificationService.NotifyResidentAsync(complaint, $"Your complaint #{complaint.ComplaintId} is now {request.NewStatus}.");

        var responses = await ToResponsesAsync(new List<Complaint> { complaint });
        return ServiceResult<ComplaintResponse>.Ok(responses[0]);
    }

    /// Admin only. Also notifies the resident — the request describes commenting and notifying as
    /// one combined action (complaints.md §4).
    public async Task<ServiceResult<ComplaintResponse>> AddCommentAsync(int complaintId, ComplaintCommentRequest request)
    {
        var complaint = await FindInSocietyAsync(complaintId);
        if (complaint is null)
        {
            return ServiceResult<ComplaintResponse>.Fail("Complaint not found.");
        }

        db.ComplaintUpdates.Add(new ComplaintUpdate
        {
            SocietyId = complaint.SocietyId,
            ComplaintId = complaint.ComplaintId,
            UpdateType = "Comment",
            CommentText = request.CommentText,
            CreatedBy = currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Comment", "Complaint", complaint.ComplaintId, request.CommentText);

        await notificationService.NotifyResidentAsync(complaint, $"New update on your complaint #{complaint.ComplaintId}.");

        var responses = await ToResponsesAsync(new List<Complaint> { complaint });
        return ServiceResult<ComplaintResponse>.Ok(responses[0]);
    }

    /// Resident only, own complaint only, only while Resolved (complaints.md §1) — non-blocking:
    /// Admin can still close the complaint directly via UpdateStatusAsync if the resident never
    /// confirms.
    public async Task<ServiceResult<ComplaintResponse>> ConfirmResolutionAsync(int complaintId)
    {
        if (currentUser.ResidentId is not { } residentId)
        {
            return ServiceResult<ComplaintResponse>.Fail("Only a resident account can confirm a resolution.");
        }

        var complaint = await FindInSocietyAsync(complaintId);
        if (complaint is null)
        {
            return ServiceResult<ComplaintResponse>.Fail("Complaint not found.");
        }

        if (complaint.ResidentId != residentId)
        {
            return ServiceResult<ComplaintResponse>.Fail("You can only confirm resolution on your own complaint.");
        }

        if (complaint.Status != ComplaintStatuses.Resolved)
        {
            return ServiceResult<ComplaintResponse>.Fail("This complaint isn't awaiting confirmation.");
        }

        complaint.ResidentConfirmed = true;
        complaint.ResidentConfirmedOn = DateTime.UtcNow;
        complaint.Status = ComplaintStatuses.Closed;
        complaint.ClosedOn = DateTime.UtcNow;
        await db.SaveChangesAsync();

        db.ComplaintUpdates.Add(new ComplaintUpdate
        {
            SocietyId = complaint.SocietyId,
            ComplaintId = complaint.ComplaintId,
            UpdateType = "StatusChange",
            OldStatus = ComplaintStatuses.Resolved,
            NewStatus = ComplaintStatuses.Closed,
            CommentText = "Resident confirmed the resolution.",
            CreatedBy = currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var responses = await ToResponsesAsync(new List<Complaint> { complaint });
        return ServiceResult<ComplaintResponse>.Ok(responses[0]);
    }

    private Task<Complaint?> FindInSocietyAsync(int complaintId) =>
        currentUser.SocietyId is { } societyId
            ? db.Complaints.FirstOrDefaultAsync(c => c.ComplaintId == complaintId && c.SocietyId == societyId)
            : Task.FromResult<Complaint?>(null);

    private async Task<List<ComplaintResponse>> ToResponsesAsync(List<Complaint> complaints)
    {
        if (complaints.Count == 0)
        {
            return new List<ComplaintResponse>();
        }

        var residentIds = complaints.Select(c => c.ResidentId).Distinct().ToList();
        var residents = await db.Residents
            .Where(r => residentIds.Contains(r.ResidentId))
            .ToDictionaryAsync(r => r.ResidentId, r => r);

        var flatIds = complaints.Where(c => c.FlatId.HasValue).Select(c => c.FlatId!.Value).Distinct().ToList();
        var flatNos = await db.Flats
            .Where(f => flatIds.Contains(f.FlatId))
            .ToDictionaryAsync(f => f.FlatId, f => f.FlatNo ?? string.Empty);

        var categoryIds = complaints.Select(c => c.CategoryId).Distinct().ToList();
        var categoryNames = await db.ComplaintCategories
            .Where(c => categoryIds.Contains(c.ComplaintCategoryId))
            .ToDictionaryAsync(c => c.ComplaintCategoryId, c => c.CategoryName);

        var createdByIds = complaints.Where(c => c.CreatedBy.HasValue).Select(c => c.CreatedBy!.Value).Distinct().ToList();
        var createdByNames = await ResolveDisplayNamesAsync(createdByIds);

        var complaintIds = complaints.Select(c => c.ComplaintId).ToList();

        var attachmentsByComplaintId = await db.ComplaintAttachments
            .Where(a => complaintIds.Contains(a.ComplaintId))
            .GroupBy(a => a.ComplaintId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(a => new ComplaintAttachmentResponse
                {
                    ComplaintAttachmentId = a.ComplaintAttachmentId,
                    AttachmentKind = a.AttachmentKind,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSizeBytes = a.FileSizeBytes,
                    UploadedOn = a.UploadedOn,
                }).ToList());

        var updates = await db.ComplaintUpdates
            .Where(u => complaintIds.Contains(u.ComplaintId))
            .OrderBy(u => u.CreatedOn)
            .ToListAsync();
        var updateAuthorIds = updates.Where(u => u.CreatedBy.HasValue).Select(u => u.CreatedBy!.Value).Distinct().ToList();
        var updateAuthorNames = await ResolveDisplayNamesAsync(updateAuthorIds);
        var updatesByComplaintId = updates
            .GroupBy(u => u.ComplaintId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(u => new ComplaintUpdateResponse
                {
                    ComplaintUpdateId = u.ComplaintUpdateId,
                    UpdateType = u.UpdateType,
                    OldStatus = u.OldStatus,
                    NewStatus = u.NewStatus,
                    CommentText = u.CommentText,
                    CreatedByName = u.CreatedBy.HasValue ? updateAuthorNames.GetValueOrDefault(u.CreatedBy.Value, string.Empty) : string.Empty,
                    CreatedOn = u.CreatedOn,
                }).ToList());

        return complaints.Select(c =>
        {
            var resident = residents.GetValueOrDefault(c.ResidentId);

            return new ComplaintResponse
            {
                ComplaintId = c.ComplaintId,
                ResidentId = c.ResidentId,
                ResidentName = resident?.FullName ?? string.Empty,
                FlatId = c.FlatId,
                FlatNo = c.FlatId.HasValue ? flatNos.GetValueOrDefault(c.FlatId.Value) : null,
                CategoryId = c.CategoryId,
                CategoryName = categoryNames.GetValueOrDefault(c.CategoryId, string.Empty),
                Description = c.Description,
                Priority = c.Priority,
                Status = c.Status,
                AssignedToName = c.AssignedToName,
                AssignedToContact = c.AssignedToContact,
                AssignmentNotes = c.AssignmentNotes,
                AssignedOn = c.AssignedOn,
                ResidentConfirmed = c.ResidentConfirmed,
                ResidentConfirmedOn = c.ResidentConfirmedOn,
                ClosedOn = c.ClosedOn,
                CreatedByName = c.CreatedBy.HasValue ? createdByNames.GetValueOrDefault(c.CreatedBy.Value, string.Empty) : resident?.FullName ?? string.Empty,
                CreatedOn = c.CreatedOn,
                IsOwnComplaint = currentUser.ResidentId.HasValue && currentUser.ResidentId == c.ResidentId,
                Attachments = attachmentsByComplaintId.GetValueOrDefault(c.ComplaintId, new List<ComplaintAttachmentResponse>()),
                Updates = updatesByComplaintId.GetValueOrDefault(c.ComplaintId, new List<ComplaintUpdateResponse>()),
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
