using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;

namespace SocietyManagementCore.Services;

/// Resolves a (ChargeTargetTypeId, TargetId) pair down to the actual set of FlatIds it applies to
/// (mm §2.4), and validates that a TargetId is a real reference within the caller's own society
/// before a rule/exemption is allowed to save — TargetId is deliberately not FK-enforced at the DB
/// level since it's polymorphic (mm §2.2), so this is the only place that check happens.
public class MaintenanceTargetResolver(SocietyDbContext db)
{
    public async Task<List<int>> ResolveFlatIdsAsync(int societyId, string chargeTargetTypeCode, int? targetId)
    {
        switch (chargeTargetTypeCode)
        {
            case "AllFlats":
                return await db.Flats.Where(f => f.SocietyId == societyId).Select(f => f.FlatId).ToListAsync();

            case "Wing":
                return await db.Flats
                    .Where(f => f.SocietyId == societyId && f.WingId == targetId)
                    .Select(f => f.FlatId)
                    .ToListAsync();

            case "FlatGroup":
                return await db.FlatGroupMembers
                    .Where(m => m.SocietyId == societyId && m.FlatGroupId == targetId)
                    .Select(m => m.FlatId)
                    .ToListAsync();

            case "SpecificFlat":
                return targetId is null
                    ? new List<int>()
                    : await db.Flats
                        .Where(f => f.SocietyId == societyId && f.FlatId == targetId)
                        .Select(f => f.FlatId)
                        .ToListAsync();

            case "ParkingType":
                return await db.Parkings
                    .Where(p => p.SocietyId == societyId && p.ParkingTypeId == targetId && p.FlatId != null)
                    .Select(p => p.FlatId!.Value)
                    .Distinct()
                    .ToListAsync();

            case "Amenity":
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                return await db.AmenitySubscriptions
                    .Where(s => s.SocietyId == societyId && s.AmenityId == targetId && s.IsActive
                        && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
                    .Select(s => s.FlatId)
                    .Distinct()
                    .ToListAsync();

            default:
                return new List<int>();
        }
    }

    /// True if TargetId is a real, in-society reference for the given target type code. AllFlats
    /// needs no TargetId at all; every other type does.
    public async Task<bool> ValidateTargetAsync(int societyId, string chargeTargetTypeCode, int? targetId)
    {
        if (chargeTargetTypeCode == "AllFlats")
        {
            return true;
        }

        if (targetId is not { } id)
        {
            return false;
        }

        return chargeTargetTypeCode switch
        {
            "Wing" => await db.Flats.AnyAsync(f => f.SocietyId == societyId && f.WingId == id),
            "FlatGroup" => await db.FlatGroups.AnyAsync(g => g.SocietyId == societyId && g.FlatGroupId == id),
            "SpecificFlat" => await db.Flats.AnyAsync(f => f.SocietyId == societyId && f.FlatId == id),
            "ParkingType" => await db.ParkingTypes.AnyAsync(p => p.SocietyId == societyId && p.ParkingTypeId == id),
            "Amenity" => await db.Amenities.AnyAsync(a => a.SocietyId == societyId && a.AmenityId == id),
            _ => false,
        };
    }
}
