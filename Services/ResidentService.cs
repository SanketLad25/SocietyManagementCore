using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class ResidentService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task<List<ResidentResponse>> ListAsync(string? search)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<ResidentResponse>();
        }

        var query = db.Residents.Where(r => r.SocietyId == societyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                (r.FullName != null && r.FullName.Contains(term)) ||
                (r.Email != null && r.Email.Contains(term)) ||
                (r.Mobile != null && r.Mobile.Contains(term)));
        }

        var residents = await query.OrderBy(r => r.FullName).ToListAsync();
        var flatNoLookup = await db.Flats
            .Where(f => f.SocietyId == societyId)
            .ToDictionaryAsync(f => f.FlatId, f => f.FlatNo);
        return residents.Select(r => ToResponse(r, flatNoLookup)).ToList();
    }

    public async Task<ResidentResponse?> GetAsync(int residentId)
    {
        var resident = await FindInSocietyAsync(residentId);
        return resident is null ? null : await ToResponseWithFlatAsync(resident);
    }

    public async Task<ServiceResult<ResidentResponse>> CreateAsync(ResidentRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<ResidentResponse>.Fail("No society context for this account.");
        }

        var emailInUse = await db.Residents.AnyAsync(r => r.SocietyId == societyId && r.Email == request.Email);
        if (emailInUse)
        {
            return ServiceResult<ResidentResponse>.Fail("A resident with this email already exists.");
        }

        var resident = new Resident
        {
            SocietyId = societyId,
            FullName = request.FullName,
            Email = request.Email,
            Mobile = request.Mobile,
            FlatId = request.FlatId,
            IsOwner = request.IsOwner,
            IsActive = request.IsActive,
        };

        db.Residents.Add(resident);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ResidentResponse>.Fail("Unable to save resident. Check that the Flat ID exists.");
        }

        return ServiceResult<ResidentResponse>.Ok(await ToResponseWithFlatAsync(resident));
    }

    public async Task<ServiceResult<ResidentResponse>> UpdateAsync(int residentId, ResidentRequest request)
    {
        var resident = await FindInSocietyAsync(residentId);
        if (resident is null)
        {
            return ServiceResult<ResidentResponse>.Fail("Resident not found.");
        }

        var emailInUse = await db.Residents.AnyAsync(r =>
            r.SocietyId == resident.SocietyId && r.Email == request.Email && r.ResidentId != residentId);
        if (emailInUse)
        {
            return ServiceResult<ResidentResponse>.Fail("A resident with this email already exists.");
        }

        resident.FullName = request.FullName;
        resident.Email = request.Email;
        resident.Mobile = request.Mobile;
        resident.FlatId = request.FlatId;
        resident.IsOwner = request.IsOwner;
        resident.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ResidentResponse>.Fail("Unable to save resident. Check that the Flat ID exists.");
        }

        return ServiceResult<ResidentResponse>.Ok(await ToResponseWithFlatAsync(resident));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int residentId)
    {
        var resident = await FindInSocietyAsync(residentId);
        if (resident is null)
        {
            return ServiceResult<bool>.Fail("Resident not found.");
        }

        db.Residents.Remove(resident);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<bool>.Fail(
                "Cannot delete this resident because linked records exist (login account, complaints, or visitor logs).");
        }

        return ServiceResult<bool>.Ok(true);
    }

    private Task<Resident?> FindInSocietyAsync(int residentId) =>
        currentUser.SocietyId is { } societyId
            ? db.Residents.FirstOrDefaultAsync(r => r.ResidentId == residentId && r.SocietyId == societyId)
            : Task.FromResult<Resident?>(null);

    private async Task<ResidentResponse> ToResponseWithFlatAsync(Resident resident)
    {
        string? flatNo = null;
        if (resident.FlatId is { } flatId)
        {
            flatNo = await db.Flats.Where(f => f.FlatId == flatId).Select(f => f.FlatNo).FirstOrDefaultAsync();
        }

        return ToResponse(resident, flatNo);
    }

    private static ResidentResponse ToResponse(Resident resident, IReadOnlyDictionary<int, string?> flatNoLookup) =>
        ToResponse(resident, resident.FlatId is { } flatId && flatNoLookup.TryGetValue(flatId, out var flatNo) ? flatNo : null);

    private static ResidentResponse ToResponse(Resident resident, string? flatNo) => new()
    {
        ResidentId = resident.ResidentId,
        FlatId = resident.FlatId,
        FlatNo = flatNo,
        FullName = resident.FullName ?? string.Empty,
        Mobile = resident.Mobile,
        Email = resident.Email,
        IsOwner = resident.IsOwner ?? false,
        IsActive = resident.IsActive ?? false,
    };
}
