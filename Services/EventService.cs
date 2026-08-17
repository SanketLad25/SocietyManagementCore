using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class EventService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog, EventNotificationService notificationService)
{
    /// Any authenticated society member sees every event (events.md — universal view access,
    /// matching Complaints). Only creating/editing is narrow.
    public async Task<List<EventResponse>> ListAsync(int? categoryId, string? status)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<EventResponse>();
        }

        var query = db.Events.Where(e => e.SocietyId == societyId);

        if (categoryId is { } cid)
        {
            query = query.Where(e => e.EventCategoryId == cid);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }
        else
        {
            query = query.Where(e => e.Status != EventStatuses.Cancelled);
        }

        var events = await query.OrderBy(e => e.StartOn).ToListAsync();
        return await ToResponsesAsync(events);
    }

    public async Task<EventResponse?> GetAsync(int eventId)
    {
        var ev = await FindInSocietyAsync(eventId);
        if (ev is null)
        {
            return null;
        }

        var responses = await ToResponsesAsync(new List<Event> { ev });
        return responses.FirstOrDefault();
    }

    public async Task<ServiceResult<EventResponse>> CreateAsync(EventRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<EventResponse>.Fail("No society context for this account.");
        }

        var categoryValid = await db.EventCategories.AnyAsync(c =>
            c.SocietyId == societyId && c.EventCategoryId == request.EventCategoryId && c.IsActive);
        if (!categoryValid)
        {
            return ServiceResult<EventResponse>.Fail("Invalid category.");
        }

        if (request.EndOn is { } endOn && endOn < request.StartOn)
        {
            return ServiceResult<EventResponse>.Fail("End time can't be before the start time.");
        }

        var ev = new Event
        {
            SocietyId = societyId,
            EventCategoryId = request.EventCategoryId,
            EventName = request.EventName,
            Description = request.Description,
            StartOn = request.StartOn,
            EndOn = request.EndOn,
            Venue = request.Venue,
            CoverEmoji = request.CoverEmoji,
            OrganizerName = request.OrganizerName,
            MaxParticipants = request.MaxParticipants,
            RegistrationRequired = request.RegistrationRequired,
            Status = EventStatuses.Published,
        };

        db.Events.Add(ev);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "Event", ev.EventId, ev.EventName);

        var responses = await ToResponsesAsync(new List<Event> { ev });
        return ServiceResult<EventResponse>.Ok(responses[0]);
    }

    public async Task<ServiceResult<EventResponse>> UpdateAsync(int eventId, EventRequest request)
    {
        var ev = await FindInSocietyAsync(eventId);
        if (ev is null)
        {
            return ServiceResult<EventResponse>.Fail("Event not found.");
        }

        if (ev.Status == EventStatuses.Cancelled)
        {
            return ServiceResult<EventResponse>.Fail("This event has been cancelled and can no longer be edited.");
        }

        var categoryValid = await db.EventCategories.AnyAsync(c =>
            c.SocietyId == ev.SocietyId && c.EventCategoryId == request.EventCategoryId && c.IsActive);
        if (!categoryValid)
        {
            return ServiceResult<EventResponse>.Fail("Invalid category.");
        }

        if (request.EndOn is { } endOn && endOn < request.StartOn)
        {
            return ServiceResult<EventResponse>.Fail("End time can't be before the start time.");
        }

        ev.EventCategoryId = request.EventCategoryId;
        ev.EventName = request.EventName;
        ev.Description = request.Description;
        ev.StartOn = request.StartOn;
        ev.EndOn = request.EndOn;
        ev.Venue = request.Venue;
        ev.CoverEmoji = request.CoverEmoji;
        ev.OrganizerName = request.OrganizerName;
        ev.MaxParticipants = request.MaxParticipants;
        ev.RegistrationRequired = request.RegistrationRequired;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "Event", ev.EventId, ev.EventName);

        var responses = await ToResponsesAsync(new List<Event> { ev });
        return ServiceResult<EventResponse>.Ok(responses[0]);
    }

    /// A distinct, heavier action from Update — not a field on the edit form. Notifies every
    /// active (Registered or Waitlisted) registrant immediately (events.md Phase 3/4 design).
    public async Task<ServiceResult<EventResponse>> CancelAsync(int eventId, EventCancelRequest request)
    {
        var ev = await FindInSocietyAsync(eventId);
        if (ev is null)
        {
            return ServiceResult<EventResponse>.Fail("Event not found.");
        }

        if (ev.Status == EventStatuses.Cancelled)
        {
            return ServiceResult<EventResponse>.Fail("This event is already cancelled.");
        }

        ev.Status = EventStatuses.Cancelled;
        ev.CancelReason = request.Reason;
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Cancel", "Event", ev.EventId, request.Reason);

        await notificationService.NotifyCancelledAsync(ev);

        var responses = await ToResponsesAsync(new List<Event> { ev });
        return ServiceResult<EventResponse>.Ok(responses[0]);
    }

    private Task<Event?> FindInSocietyAsync(int eventId) =>
        currentUser.SocietyId is { } societyId
            ? db.Events.FirstOrDefaultAsync(e => e.EventId == eventId && e.SocietyId == societyId)
            : Task.FromResult<Event?>(null);

    private async Task<List<EventResponse>> ToResponsesAsync(List<Event> events)
    {
        if (events.Count == 0)
        {
            return new List<EventResponse>();
        }

        var categoryIds = events.Select(e => e.EventCategoryId).Distinct().ToList();
        var categoryNames = await db.EventCategories
            .Where(c => categoryIds.Contains(c.EventCategoryId))
            .ToDictionaryAsync(c => c.EventCategoryId, c => c.CategoryName);

        var eventIds = events.Select(e => e.EventId).ToList();
        var registrations = await db.EventRegistrations
            .Where(r => eventIds.Contains(r.EventId))
            .ToListAsync();

        var registeredCounts = registrations
            .Where(r => r.Status == EventRegistrationStatuses.Registered)
            .GroupBy(r => r.EventId)
            .ToDictionary(g => g.Key, g => g.Count());

        var waitlistedCounts = registrations
            .Where(r => r.Status == EventRegistrationStatuses.Waitlisted)
            .GroupBy(r => r.EventId)
            .ToDictionary(g => g.Key, g => g.Count());

        var myRegistrations = currentUser.ResidentId is { } myResidentId
            ? registrations.Where(r => r.ResidentId == myResidentId).ToDictionary(r => r.EventId, r => r)
            : new Dictionary<int, EventRegistration>();

        return events.Select(e =>
        {
            myRegistrations.TryGetValue(e.EventId, out var mine);
            return new EventResponse
            {
                EventId = e.EventId,
                EventCategoryId = e.EventCategoryId,
                CategoryName = categoryNames.GetValueOrDefault(e.EventCategoryId, string.Empty),
                EventName = e.EventName,
                Description = e.Description,
                StartOn = e.StartOn,
                EndOn = e.EndOn,
                Venue = e.Venue,
                CoverEmoji = e.CoverEmoji,
                CoverImagePath = e.CoverImagePath,
                OrganizerName = e.OrganizerName,
                MaxParticipants = e.MaxParticipants,
                RegistrationRequired = e.RegistrationRequired,
                Status = e.Status,
                CancelReason = e.CancelReason,
                CreatedOn = e.CreatedOn,
                RegisteredCount = registeredCounts.GetValueOrDefault(e.EventId),
                WaitlistedCount = waitlistedCounts.GetValueOrDefault(e.EventId),
                MyRegistrationStatus = mine?.Status,
                MyRegistrationCode = mine?.RegistrationCode,
                MyParticipantCount = mine?.ParticipantCount,
            };
        }).ToList();
    }
}
