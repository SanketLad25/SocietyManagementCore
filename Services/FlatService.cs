using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class FlatService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task<List<FlatResponse>> ListAsync(string? search)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<FlatResponse>();
        }

        var query = db.Flats.Where(f => f.SocietyId == societyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(f => f.FlatNo != null && f.FlatNo.Contains(term));
        }

        var flats = await query.OrderBy(f => f.FlatNo).ToListAsync();
        return flats.Select(ToResponse).ToList();
    }

    public async Task<FlatResponse?> GetAsync(int flatId)
    {
        var flat = await FindInSocietyAsync(flatId);
        return flat is null ? null : ToResponse(flat);
    }

    public async Task<ServiceResult<FlatResponse>> CreateAsync(FlatRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<FlatResponse>.Fail("No society context for this account.");
        }

        var flatNoInUse = await db.Flats.AnyAsync(f => f.SocietyId == societyId && f.FlatNo == request.FlatNo);
        if (flatNoInUse)
        {
            return ServiceResult<FlatResponse>.Fail("A flat with this Flat No already exists.");
        }

        var flat = new Flat
        {
            SocietyId = societyId,
            WingId = request.WingId,
            FlatNo = request.FlatNo,
            FloorNo = request.FloorNo,
            AreaSqFt = request.AreaSqFt,
            MaintenanceAmount = request.MaintenanceAmount,
        };

        db.Flats.Add(flat);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<FlatResponse>.Fail("Unable to save flat. Check that the Wing ID exists.");
        }

        return ServiceResult<FlatResponse>.Ok(ToResponse(flat));
    }

    public async Task<ServiceResult<FlatResponse>> UpdateAsync(int flatId, FlatRequest request)
    {
        var flat = await FindInSocietyAsync(flatId);
        if (flat is null)
        {
            return ServiceResult<FlatResponse>.Fail("Flat not found.");
        }

        var flatNoInUse = await db.Flats.AnyAsync(f =>
            f.SocietyId == flat.SocietyId && f.FlatNo == request.FlatNo && f.FlatId != flatId);
        if (flatNoInUse)
        {
            return ServiceResult<FlatResponse>.Fail("A flat with this Flat No already exists.");
        }

        flat.WingId = request.WingId;
        flat.FlatNo = request.FlatNo;
        flat.FloorNo = request.FloorNo;
        flat.AreaSqFt = request.AreaSqFt;
        flat.MaintenanceAmount = request.MaintenanceAmount;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<FlatResponse>.Fail("Unable to save flat. Check that the Wing ID exists.");
        }

        return ServiceResult<FlatResponse>.Ok(ToResponse(flat));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int flatId)
    {
        var flat = await FindInSocietyAsync(flatId);
        if (flat is null)
        {
            return ServiceResult<bool>.Fail("Flat not found.");
        }

        db.Flats.Remove(flat);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<bool>.Fail(
                "Cannot delete this flat because linked records exist (residents, bills, or parking).");
        }

        return ServiceResult<bool>.Ok(true);
    }

    private Task<Flat?> FindInSocietyAsync(int flatId) =>
        currentUser.SocietyId is { } societyId
            ? db.Flats.FirstOrDefaultAsync(f => f.FlatId == flatId && f.SocietyId == societyId)
            : Task.FromResult<Flat?>(null);

    private static FlatResponse ToResponse(Flat flat) => new()
    {
        FlatId = flat.FlatId,
        WingId = flat.WingId,
        FlatNo = flat.FlatNo ?? string.Empty,
        FloorNo = flat.FloorNo,
        AreaSqFt = flat.AreaSqFt,
        MaintenanceAmount = flat.MaintenanceAmount,
    };
}
