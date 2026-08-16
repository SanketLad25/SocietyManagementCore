using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Flat-wise vehicle/parking-slot assignments. This is the data MaintenanceTargetResolver and
/// PerUnitStrategy already read to bill each flat by its actual parking-type counts (mm) — this
/// service is what actually populates it; the billing logic doesn't change here.
public class ParkingService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<ParkingResponse>> ListAsync(string? search)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<ParkingResponse>();
        }

        var query =
            from p in db.Parkings
            join f in db.Flats on p.FlatId equals f.FlatId
            join t in db.ParkingTypes on p.ParkingTypeId equals t.ParkingTypeId
            where p.SocietyId == societyId
            select new { Parking = p, FlatNo = f.FlatNo, TypeName = t.TypeName };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                (x.FlatNo != null && x.FlatNo.Contains(term)) ||
                (x.Parking.VehicleNo != null && x.Parking.VehicleNo.Contains(term)) ||
                (x.Parking.ParkingNo != null && x.Parking.ParkingNo.Contains(term)));
        }

        var rows = await query.OrderBy(x => x.FlatNo).ThenBy(x => x.TypeName).ToListAsync();

        return rows.Select(x => ToResponse(x.Parking, x.FlatNo ?? string.Empty, x.TypeName ?? string.Empty)).ToList();
    }

    public async Task<ServiceResult<ParkingResponse>> CreateAsync(ParkingRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<ParkingResponse>.Fail("No society context for this account.");
        }

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == request.FlatId && f.SocietyId == societyId);
        if (flat is null)
        {
            return ServiceResult<ParkingResponse>.Fail("Flat not found in this society.");
        }

        var parkingType = await db.ParkingTypes.FirstOrDefaultAsync(t =>
            t.ParkingTypeId == request.ParkingTypeId && t.SocietyId == societyId);
        if (parkingType is null)
        {
            return ServiceResult<ParkingResponse>.Fail("Parking type not found in this society.");
        }

        var parking = new Parking
        {
            SocietyId = societyId,
            FlatId = request.FlatId,
            ParkingTypeId = request.ParkingTypeId,
            ParkingNo = request.ParkingNo,
            VehicleNo = request.VehicleNo,
            VehicleType = request.VehicleType,
        };

        db.Parkings.Add(parking);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "Parking", parking.ParkingId, $"FlatId={request.FlatId}");

        return ServiceResult<ParkingResponse>.Ok(ToResponse(parking, flat.FlatNo ?? string.Empty, parkingType.TypeName));
    }

    public async Task<ServiceResult<ParkingResponse>> UpdateAsync(int parkingId, ParkingRequest request)
    {
        var parking = await FindInSocietyAsync(parkingId);
        if (parking is null)
        {
            return ServiceResult<ParkingResponse>.Fail("Parking record not found.");
        }

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == request.FlatId && f.SocietyId == parking.SocietyId);
        if (flat is null)
        {
            return ServiceResult<ParkingResponse>.Fail("Flat not found in this society.");
        }

        var parkingType = await db.ParkingTypes.FirstOrDefaultAsync(t =>
            t.ParkingTypeId == request.ParkingTypeId && t.SocietyId == parking.SocietyId);
        if (parkingType is null)
        {
            return ServiceResult<ParkingResponse>.Fail("Parking type not found in this society.");
        }

        parking.FlatId = request.FlatId;
        parking.ParkingTypeId = request.ParkingTypeId;
        parking.ParkingNo = request.ParkingNo;
        parking.VehicleNo = request.VehicleNo;
        parking.VehicleType = request.VehicleType;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "Parking", parking.ParkingId, $"FlatId={request.FlatId}");

        return ServiceResult<ParkingResponse>.Ok(ToResponse(parking, flat.FlatNo ?? string.Empty, parkingType.TypeName));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int parkingId)
    {
        var parking = await FindInSocietyAsync(parkingId);
        if (parking is null)
        {
            return ServiceResult<bool>.Fail("Parking record not found.");
        }

        db.Parkings.Remove(parking);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Delete", "Parking", parking.ParkingId);

        return ServiceResult<bool>.Ok(true);
    }

    private Task<Parking?> FindInSocietyAsync(int parkingId) =>
        currentUser.SocietyId is { } societyId
            ? db.Parkings.FirstOrDefaultAsync(p => p.ParkingId == parkingId && p.SocietyId == societyId)
            : Task.FromResult<Parking?>(null);

    private static ParkingResponse ToResponse(Parking parking, string flatNo, string parkingTypeName) => new()
    {
        ParkingId = parking.ParkingId,
        FlatId = parking.FlatId ?? 0,
        FlatNo = flatNo,
        ParkingTypeId = parking.ParkingTypeId ?? 0,
        ParkingTypeName = parkingTypeName,
        ParkingNo = parking.ParkingNo,
        VehicleNo = parking.VehicleNo,
        VehicleType = parking.VehicleType,
    };
}
