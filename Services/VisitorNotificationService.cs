using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Per-recipient notification feed, mirrors ComplaintNotificationService. NotifyFlatResidentsAsync
/// uses the multi-recipient loop shape (ComplaintNotificationService.NotifyManagersAsync's pattern)
/// since a flat can have several residents, unlike a single-resident notify.
public class VisitorNotificationService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task NotifyFlatResidentsAsync(VisitorLog log, string message)
    {
        var recipientUserIds = await (
            from u in db.UserLogins
            join r in db.Residents on u.ResidentId equals r.ResidentId
            where u.SocietyId == log.SocietyId && r.FlatId == log.FlatId
            select u.UserId
        ).Distinct().ToListAsync();

        foreach (var userId in recipientUserIds)
        {
            db.VisitorNotifications.Add(new VisitorNotification
            {
                SocietyId = log.SocietyId,
                VisitorLogId = log.VisitorLogId,
                RecipientUserId = userId,
                Message = message,
                CreatedOn = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task NotifyUserAsync(VisitorLog log, int recipientUserId, string message)
    {
        db.VisitorNotifications.Add(new VisitorNotification
        {
            SocietyId = log.SocietyId,
            VisitorLogId = log.VisitorLogId,
            RecipientUserId = recipientUserId,
            Message = message,
            CreatedOn = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public async Task<List<VisitorNotificationResponse>> ListMineAsync()
    {
        if (currentUser.UserId is not { } userId)
        {
            return new List<VisitorNotificationResponse>();
        }

        var notifications = await db.VisitorNotifications
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedOn)
            .ToListAsync();

        return notifications.Select(n => new VisitorNotificationResponse
        {
            VisitorNotificationId = n.VisitorNotificationId,
            VisitorLogId = n.VisitorLogId,
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

        var notification = await db.VisitorNotifications.FirstOrDefaultAsync(n =>
            n.VisitorNotificationId == notificationId && n.RecipientUserId == userId);
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
}
