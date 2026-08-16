using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class AmenitySubscriptionService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<AmenitySubscriptionResponse>> ListAsync(int amenityId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<AmenitySubscriptionResponse>();
        }

        var subscriptions = await (
            from s in db.AmenitySubscriptions
            join f in db.Flats on s.FlatId equals f.FlatId
            where s.SocietyId == societyId && s.AmenityId == amenityId
            orderby s.EffectiveFrom descending
            select new AmenitySubscriptionResponse
            {
                SubscriptionId = s.SubscriptionId,
                AmenityId = s.AmenityId,
                FlatId = s.FlatId,
                FlatNo = f.FlatNo ?? string.Empty,
                EffectiveFrom = s.EffectiveFrom,
                EffectiveTo = s.EffectiveTo,
                IsActive = s.IsActive,
            }).ToListAsync();

        return subscriptions;
    }

    public async Task<ServiceResult<AmenitySubscriptionResponse>> CreateAsync(int amenityId, AmenitySubscriptionRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<AmenitySubscriptionResponse>.Fail("No society context for this account.");
        }

        var amenityExists = await db.Amenities.AnyAsync(a => a.AmenityId == amenityId && a.SocietyId == societyId);
        if (!amenityExists)
        {
            return ServiceResult<AmenitySubscriptionResponse>.Fail("Amenity not found.");
        }

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == request.FlatId && f.SocietyId == societyId);
        if (flat is null)
        {
            return ServiceResult<AmenitySubscriptionResponse>.Fail("Flat not found in this society.");
        }

        var alreadySubscribed = await db.AmenitySubscriptions.AnyAsync(s =>
            s.SocietyId == societyId && s.AmenityId == amenityId && s.FlatId == request.FlatId &&
            s.IsActive && s.EffectiveTo == null);
        if (alreadySubscribed)
        {
            return ServiceResult<AmenitySubscriptionResponse>.Fail("This flat already has an active subscription to this amenity.");
        }

        var subscription = new AmenitySubscription
        {
            SocietyId = societyId,
            AmenityId = amenityId,
            FlatId = request.FlatId,
            EffectiveFrom = request.EffectiveFrom,
        };

        db.AmenitySubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "AmenitySubscription", subscription.SubscriptionId, $"FlatId={request.FlatId}");

        return ServiceResult<AmenitySubscriptionResponse>.Ok(new AmenitySubscriptionResponse
        {
            SubscriptionId = subscription.SubscriptionId,
            AmenityId = subscription.AmenityId,
            FlatId = subscription.FlatId,
            FlatNo = flat.FlatNo ?? string.Empty,
            EffectiveFrom = subscription.EffectiveFrom,
            EffectiveTo = subscription.EffectiveTo,
            IsActive = subscription.IsActive,
        });
    }

    /// Ends a subscription as of a date rather than deleting it — preserves history for any bill
    /// already generated while it was active (mm §2.3's immutable-snapshot philosophy).
    public async Task<ServiceResult<bool>> CancelAsync(int amenityId, int subscriptionId, DateOnly effectiveTo)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<bool>.Fail("No society context for this account.");
        }

        var subscription = await db.AmenitySubscriptions.FirstOrDefaultAsync(s =>
            s.SubscriptionId == subscriptionId && s.AmenityId == amenityId && s.SocietyId == societyId);

        if (subscription is null)
        {
            return ServiceResult<bool>.Fail("Subscription not found.");
        }

        subscription.EffectiveTo = effectiveTo;
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Cancel", "AmenitySubscription", subscription.SubscriptionId);

        return ServiceResult<bool>.Ok(true);
    }
}
