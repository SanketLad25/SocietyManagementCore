using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Per-recipient notification feed (complaints.md §2.5/§4) — distinct from the Notice bell's
/// broadcast-to-everyone model. One row per (complaint, recipient, event).
public class ComplaintNotificationService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task NotifyManagersAsync(Complaint complaint)
    {
        var managerRoleIds = await db.Roles
            .Where(r => r.RoleName == RoleNames.Admin || r.RoleName == RoleNames.Secretary || r.RoleName == RoleNames.Chairman)
            .Select(r => r.RoleId)
            .ToListAsync();

        var recipientUserIds = await db.UserLogins
            .Where(u => u.SocietyId == complaint.SocietyId && u.RoleId.HasValue && managerRoleIds.Contains(u.RoleId.Value))
            .Select(u => u.UserId)
            .ToListAsync();

        var message = $"New complaint raised: {await ResolveCategoryNameAsync(complaint.CategoryId)}" +
            (complaint.FlatId.HasValue ? $" — Flat {await ResolveFlatNoAsync(complaint.FlatId.Value)}" : "");

        foreach (var userId in recipientUserIds)
        {
            db.ComplaintNotifications.Add(new ComplaintNotification
            {
                SocietyId = complaint.SocietyId,
                ComplaintId = complaint.ComplaintId,
                RecipientUserId = userId,
                Message = message,
                CreatedOn = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task NotifyResidentAsync(Complaint complaint, string message)
    {
        var recipientUserId = await db.UserLogins
            .Where(u => u.ResidentId == complaint.ResidentId && u.SocietyId == complaint.SocietyId)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync();

        if (recipientUserId == 0)
        {
            return;
        }

        db.ComplaintNotifications.Add(new ComplaintNotification
        {
            SocietyId = complaint.SocietyId,
            ComplaintId = complaint.ComplaintId,
            RecipientUserId = recipientUserId,
            Message = message,
            CreatedOn = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public async Task<List<ComplaintNotificationResponse>> ListMineAsync()
    {
        if (currentUser.UserId is not { } userId)
        {
            return new List<ComplaintNotificationResponse>();
        }

        var notifications = await db.ComplaintNotifications
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedOn)
            .ToListAsync();

        return notifications.Select(n => new ComplaintNotificationResponse
        {
            ComplaintNotificationId = n.ComplaintNotificationId,
            ComplaintId = n.ComplaintId,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedOn = n.CreatedOn,
        }).ToList();
    }

    public async Task<ServiceResult<bool>> MarkReadAsync(int notificationId)
    {
        if (currentUser.UserId is not { } userId)
        {
            return ServiceResult<bool>.Fail("Not authenticated.");
        }

        var notification = await db.ComplaintNotifications.FirstOrDefaultAsync(n =>
            n.ComplaintNotificationId == notificationId && n.RecipientUserId == userId);
        if (notification is null)
        {
            return ServiceResult<bool>.Fail("Notification not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync();
        }

        return ServiceResult<bool>.Ok(true);
    }

    private async Task<string> ResolveCategoryNameAsync(int categoryId) =>
        await db.ComplaintCategories.Where(c => c.ComplaintCategoryId == categoryId).Select(c => c.CategoryName).FirstOrDefaultAsync()
        ?? string.Empty;

    private async Task<string> ResolveFlatNoAsync(int flatId) =>
        await db.Flats.Where(f => f.FlatId == flatId).Select(f => f.FlatNo).FirstOrDefaultAsync() ?? string.Empty;
}
