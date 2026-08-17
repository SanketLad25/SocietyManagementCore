using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Per-recipient notification feed (events.md — Phase 3/4 technical design), mirroring
/// ComplaintNotificationService exactly: one row per (event, recipient). Triggers wired up so far:
/// event cancelled (every active registrant) and a resident promoted off the waitlist.
public class EventNotificationService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task NotifyCancelledAsync(Event ev)
    {
        var recipientUserIds = await db.EventRegistrations
            .Where(r => r.EventId == ev.EventId && (r.Status == EventRegistrationStatuses.Registered || r.Status == EventRegistrationStatuses.Waitlisted))
            .Join(db.UserLogins, r => r.ResidentId, u => u.ResidentId, (r, u) => u.UserId)
            .ToListAsync();

        var message = $"{ev.EventName} has been cancelled.";

        foreach (var userId in recipientUserIds)
        {
            db.EventNotifications.Add(new EventNotification
            {
                SocietyId = ev.SocietyId,
                EventId = ev.EventId,
                RecipientUserId = userId,
                Message = message,
                CreatedOn = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task NotifyPromotedAsync(Event ev, int residentId)
    {
        var recipientUserId = await db.UserLogins
            .Where(u => u.ResidentId == residentId && u.SocietyId == ev.SocietyId)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync();

        if (recipientUserId == 0)
        {
            return;
        }

        db.EventNotifications.Add(new EventNotification
        {
            SocietyId = ev.SocietyId,
            EventId = ev.EventId,
            RecipientUserId = recipientUserId,
            Message = $"You're off the waitlist for {ev.EventName} — you're now registered.",
            CreatedOn = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public async Task<List<EventNotificationResponse>> ListMineAsync()
    {
        if (currentUser.UserId is not { } userId)
        {
            return new List<EventNotificationResponse>();
        }

        var notifications = await db.EventNotifications
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedOn)
            .ToListAsync();

        return notifications.Select(n => new EventNotificationResponse
        {
            EventNotificationId = n.EventNotificationId,
            EventId = n.EventId,
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

        var notification = await db.EventNotifications.FirstOrDefaultAsync(n =>
            n.EventNotificationId == notificationId && n.RecipientUserId == userId);
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
