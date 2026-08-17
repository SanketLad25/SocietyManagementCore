using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class EventRegistrationService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog, EventNotificationService notificationService)
{
    /// Resident only. Upserts the one row this resident can have per event (unique on
    /// EventId+ResidentId) — deterministic RegistrationCode derived from the two IDs, so it's
    /// stable across re-registration and needs no collision retry.
    public async Task<ServiceResult<bool>> JoinAsync(int eventId, EventRegistrationRequest request)
    {
        if (currentUser.ResidentId is not { } residentId)
        {
            return ServiceResult<bool>.Fail("Only a resident account can join an event.");
        }

        var ev = await FindEventInSocietyAsync(eventId);
        if (ev is null)
        {
            return ServiceResult<bool>.Fail("Event not found.");
        }

        if (ev.Status == EventStatuses.Cancelled)
        {
            return ServiceResult<bool>.Fail("This event has been cancelled.");
        }

        var resident = await db.Residents.FirstOrDefaultAsync(r => r.ResidentId == residentId);
        if (resident?.FlatId is not { } flatId)
        {
            return ServiceResult<bool>.Fail("Your account has no flat on record.");
        }

        var registration = await db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.ResidentId == residentId);

        var excludeId = registration?.EventRegistrationId ?? 0;
        var registeredCount = await db.EventRegistrations.CountAsync(r =>
            r.EventId == eventId && r.Status == EventRegistrationStatuses.Registered && r.EventRegistrationId != excludeId);

        var status = ev.MaxParticipants is { } max && registeredCount >= max
            ? EventRegistrationStatuses.Waitlisted
            : EventRegistrationStatuses.Registered;

        if (registration is null)
        {
            registration = new EventRegistration
            {
                SocietyId = ev.SocietyId,
                EventId = eventId,
                ResidentId = residentId,
                FlatId = flatId,
                RegistrationCode = $"REG-{(eventId * 100000 + residentId) % 1000000:D6}",
            };
            db.EventRegistrations.Add(registration);
        }

        registration.ParticipantCount = request.ParticipantCount;
        registration.Comments = request.Comments;
        registration.Status = status;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Join", "Event", eventId, $"{status} ({request.ParticipantCount} participant(s))");

        return ServiceResult<bool>.Ok(true);
    }

    /// Resident only. A no-op if they're already Registered/Waitlisted — tapping "Interested"
    /// shouldn't downgrade an existing commitment.
    public async Task<ServiceResult<bool>> MarkInterestedAsync(int eventId)
    {
        if (currentUser.ResidentId is not { } residentId)
        {
            return ServiceResult<bool>.Fail("Only a resident account can do this.");
        }

        var ev = await FindEventInSocietyAsync(eventId);
        if (ev is null)
        {
            return ServiceResult<bool>.Fail("Event not found.");
        }

        var resident = await db.Residents.FirstOrDefaultAsync(r => r.ResidentId == residentId);
        if (resident?.FlatId is not { } flatId)
        {
            return ServiceResult<bool>.Fail("Your account has no flat on record.");
        }

        var registration = await db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.ResidentId == residentId);

        if (registration is { Status: EventRegistrationStatuses.Registered or EventRegistrationStatuses.Waitlisted })
        {
            return ServiceResult<bool>.Ok(true);
        }

        if (registration is null)
        {
            registration = new EventRegistration
            {
                SocietyId = ev.SocietyId,
                EventId = eventId,
                ResidentId = residentId,
                FlatId = flatId,
                RegistrationCode = $"REG-{(eventId * 100000 + residentId) % 1000000:D6}",
            };
            db.EventRegistrations.Add(registration);
        }

        registration.Status = EventRegistrationStatuses.Interested;
        await db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    /// Resident only, own registration only. Frees the capacity slot but does not auto-promote
    /// anyone off the waitlist — that's a deliberate manual action on the Manage Event screen
    /// (events.md — "promotion is manual, not automatic").
    public async Task<ServiceResult<bool>> CancelMyRegistrationAsync(int eventId)
    {
        if (currentUser.ResidentId is not { } residentId)
        {
            return ServiceResult<bool>.Fail("Only a resident account can do this.");
        }

        var registration = await db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.ResidentId == residentId);

        if (registration is null || registration.Status is EventRegistrationStatuses.CancelledByResident or EventRegistrationStatuses.CancelledByOrganizer)
        {
            return ServiceResult<bool>.Fail("You don't have a registration for this event.");
        }

        registration.Status = EventRegistrationStatuses.CancelledByResident;
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Cancel", "Event", eventId, "Resident cancelled their own registration");

        return ServiceResult<bool>.Ok(true);
    }

    /// Manager-only (enforced on the controller). Deliberately manual — never automatic — so an
    /// organizer reviews who's next rather than a background process silently reassigning a spot
    /// (events.md Phase 3/4 design).
    public async Task<ServiceResult<bool>> PromoteAsync(int eventId, int registrationId)
    {
        var ev = await FindEventInSocietyAsync(eventId);
        if (ev is null)
        {
            return ServiceResult<bool>.Fail("Event not found.");
        }

        var registration = await db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventRegistrationId == registrationId && r.EventId == eventId);
        if (registration is null || registration.Status != EventRegistrationStatuses.Waitlisted)
        {
            return ServiceResult<bool>.Fail("This registration isn't on the waitlist.");
        }

        var registeredCount = await db.EventRegistrations.CountAsync(r =>
            r.EventId == eventId && r.Status == EventRegistrationStatuses.Registered);
        if (ev.MaxParticipants is { } max && registeredCount >= max)
        {
            return ServiceResult<bool>.Fail("Event is already at capacity — cancel a registered spot first.");
        }

        registration.Status = EventRegistrationStatuses.Registered;
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Promote", "Event", eventId, $"Promoted registration #{registrationId} from waitlist");

        await notificationService.NotifyPromotedAsync(ev, registration.ResidentId);

        return ServiceResult<bool>.Ok(true);
    }

    /// Manager-only (enforced on the controller) — the roster of who's coming, not exposed to
    /// residents generally. Excludes Interested-only rows; those aren't a commitment worth an
    /// organizer acting on yet.
    public async Task<List<EventRegistrationResponse>> ListForEventAsync(int eventId)
    {
        var ev = await FindEventInSocietyAsync(eventId);
        if (ev is null)
        {
            return new List<EventRegistrationResponse>();
        }

        var registrations = await db.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status != EventRegistrationStatuses.Interested)
            .OrderBy(r => r.CreatedOn)
            .ToListAsync();

        if (registrations.Count == 0)
        {
            return new List<EventRegistrationResponse>();
        }

        var residentIds = registrations.Select(r => r.ResidentId).Distinct().ToList();
        var residentNames = await db.Residents
            .Where(r => residentIds.Contains(r.ResidentId))
            .ToDictionaryAsync(r => r.ResidentId, r => r.FullName);

        var flatIds = registrations.Select(r => r.FlatId).Distinct().ToList();
        var flatNos = await db.Flats
            .Where(f => flatIds.Contains(f.FlatId))
            .ToDictionaryAsync(f => f.FlatId, f => f.FlatNo);

        return registrations.Select(r => new EventRegistrationResponse
        {
            EventRegistrationId = r.EventRegistrationId,
            ResidentName = residentNames.GetValueOrDefault(r.ResidentId, string.Empty) ?? string.Empty,
            FlatNo = flatNos.GetValueOrDefault(r.FlatId),
            ParticipantCount = r.ParticipantCount,
            Comments = r.Comments,
            Status = r.Status,
            RegistrationCode = r.RegistrationCode,
            CreatedOn = r.CreatedOn,
        }).ToList();
    }

    private Task<Event?> FindEventInSocietyAsync(int eventId) =>
        currentUser.SocietyId is { } societyId
            ? db.Events.FirstOrDefaultAsync(e => e.EventId == eventId && e.SocietyId == societyId)
            : Task.FromResult<Event?>(null);
}
