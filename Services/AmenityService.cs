using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class AmenityService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<AmenityResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<AmenityResponse>();
        }

        var amenities = await db.Amenities
            .Where(a => a.SocietyId == societyId)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.AmenityName)
            .ToListAsync();

        return amenities.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<AmenityResponse>> CreateAsync(AmenityRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<AmenityResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.Amenities.AnyAsync(a => a.SocietyId == societyId && a.AmenityName == request.AmenityName);
        if (nameInUse)
        {
            return ServiceResult<AmenityResponse>.Fail("An amenity with this name already exists.");
        }

        var amenity = new Amenity
        {
            SocietyId = societyId,
            AmenityName = request.AmenityName,
            Description = request.Description,
            RequiresOptIn = request.RequiresOptIn,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.Amenities.Add(amenity);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "Amenity", amenity.AmenityId, amenity.AmenityName);

        return ServiceResult<AmenityResponse>.Ok(ToResponse(amenity));
    }

    public async Task<ServiceResult<AmenityResponse>> UpdateAsync(int amenityId, AmenityRequest request)
    {
        var amenity = await FindInSocietyAsync(amenityId);
        if (amenity is null)
        {
            return ServiceResult<AmenityResponse>.Fail("Amenity not found.");
        }

        var nameInUse = await db.Amenities.AnyAsync(a =>
            a.SocietyId == amenity.SocietyId && a.AmenityName == request.AmenityName && a.AmenityId != amenityId);
        if (nameInUse)
        {
            return ServiceResult<AmenityResponse>.Fail("An amenity with this name already exists.");
        }

        amenity.AmenityName = request.AmenityName;
        amenity.Description = request.Description;
        amenity.RequiresOptIn = request.RequiresOptIn;
        amenity.DisplayOrder = request.DisplayOrder;
        amenity.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "Amenity", amenity.AmenityId, amenity.AmenityName);

        return ServiceResult<AmenityResponse>.Ok(ToResponse(amenity));
    }

    private Task<Amenity?> FindInSocietyAsync(int amenityId) =>
        currentUser.SocietyId is { } societyId
            ? db.Amenities.FirstOrDefaultAsync(a => a.AmenityId == amenityId && a.SocietyId == societyId)
            : Task.FromResult<Amenity?>(null);

    private static AmenityResponse ToResponse(Amenity amenity) => new()
    {
        AmenityId = amenity.AmenityId,
        AmenityName = amenity.AmenityName,
        Description = amenity.Description,
        RequiresOptIn = amenity.RequiresOptIn,
        DisplayOrder = amenity.DisplayOrder,
        IsActive = amenity.IsActive,
    };
}
