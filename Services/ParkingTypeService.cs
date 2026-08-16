using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class ParkingTypeService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<ParkingTypeResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<ParkingTypeResponse>();
        }

        var parkingTypes = await db.ParkingTypes
            .Where(p => p.SocietyId == societyId)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.TypeName)
            .ToListAsync();

        return parkingTypes.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<ParkingTypeResponse>> CreateAsync(ParkingTypeRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<ParkingTypeResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.ParkingTypes.AnyAsync(p => p.SocietyId == societyId && p.TypeName == request.TypeName);
        if (nameInUse)
        {
            return ServiceResult<ParkingTypeResponse>.Fail("A parking type with this name already exists.");
        }

        var parkingType = new ParkingType
        {
            SocietyId = societyId,
            TypeName = request.TypeName,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.ParkingTypes.Add(parkingType);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "ParkingType", parkingType.ParkingTypeId, parkingType.TypeName);

        return ServiceResult<ParkingTypeResponse>.Ok(ToResponse(parkingType));
    }

    public async Task<ServiceResult<ParkingTypeResponse>> UpdateAsync(int parkingTypeId, ParkingTypeRequest request)
    {
        var parkingType = await FindInSocietyAsync(parkingTypeId);
        if (parkingType is null)
        {
            return ServiceResult<ParkingTypeResponse>.Fail("Parking type not found.");
        }

        var nameInUse = await db.ParkingTypes.AnyAsync(p =>
            p.SocietyId == parkingType.SocietyId && p.TypeName == request.TypeName && p.ParkingTypeId != parkingTypeId);
        if (nameInUse)
        {
            return ServiceResult<ParkingTypeResponse>.Fail("A parking type with this name already exists.");
        }

        parkingType.TypeName = request.TypeName;
        parkingType.DisplayOrder = request.DisplayOrder;
        parkingType.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "ParkingType", parkingType.ParkingTypeId, parkingType.TypeName);

        return ServiceResult<ParkingTypeResponse>.Ok(ToResponse(parkingType));
    }

    private Task<ParkingType?> FindInSocietyAsync(int parkingTypeId) =>
        currentUser.SocietyId is { } societyId
            ? db.ParkingTypes.FirstOrDefaultAsync(p => p.ParkingTypeId == parkingTypeId && p.SocietyId == societyId)
            : Task.FromResult<ParkingType?>(null);

    private static ParkingTypeResponse ToResponse(ParkingType parkingType) => new()
    {
        ParkingTypeId = parkingType.ParkingTypeId,
        TypeName = parkingType.TypeName,
        DisplayOrder = parkingType.DisplayOrder,
        IsActive = parkingType.IsActive,
    };
}
