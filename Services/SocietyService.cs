using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// SuperAdmin-only: onboarding new societies and each one's first Admin. This is the bootstrap
/// path that replaces public self-registration.
public class SocietyService(SocietyDbContext db, AdminUserService adminUserService)
{
    public async Task<List<SocietyResponse>> ListAsync()
    {
        var societies = await db.Societies.OrderBy(s => s.SocietyName).ToListAsync();
        return societies.Select(ToResponse).ToList();
    }

    public async Task<SocietyResponse?> GetAsync(int societyId)
    {
        var society = await db.Societies.FirstOrDefaultAsync(s => s.SocietyId == societyId);
        return society is null ? null : ToResponse(society);
    }

    public async Task<ServiceResult<SocietyResponse>> CreateAsync(SocietyRequest request)
    {
        var nameInUse = await db.Societies.AnyAsync(s => s.SocietyName == request.SocietyName);
        if (nameInUse)
        {
            return ServiceResult<SocietyResponse>.Fail("A society with this name already exists.");
        }

        var society = new Society
        {
            SocietyName = request.SocietyName,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PinCode = request.PinCode,
            RegistrationNo = request.RegistrationNo,
            BankName = request.BankName,
            BankAccountNumber = request.BankAccountNumber,
            BankIfsc = request.BankIfsc,
            ChequePayeeName = request.ChequePayeeName,
            ContactNumber = request.ContactNumber,
            InterestOnArrearsPercent = request.InterestOnArrearsPercent,
        };
        db.Societies.Add(society);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<SocietyResponse>.Fail("Unable to save society.");
        }

        await MaintenanceReferenceSeeder.SeedSocietyDefaultsAsync(db, society.SocietyId);
        await NoticeReferenceSeeder.SeedSocietyDefaultsAsync(db, society.SocietyId);
        await ComplaintReferenceSeeder.SeedSocietyDefaultsAsync(db, society.SocietyId);

        return ServiceResult<SocietyResponse>.Ok(ToResponse(society));
    }

    public async Task<ServiceResult<SocietyResponse>> UpdateAsync(int societyId, SocietyRequest request)
    {
        var society = await db.Societies.FirstOrDefaultAsync(s => s.SocietyId == societyId);
        if (society is null)
        {
            return ServiceResult<SocietyResponse>.Fail("Society not found.");
        }

        var nameInUse = await db.Societies.AnyAsync(s => s.SocietyName == request.SocietyName && s.SocietyId != societyId);
        if (nameInUse)
        {
            return ServiceResult<SocietyResponse>.Fail("A society with this name already exists.");
        }

        society.SocietyName = request.SocietyName;
        society.Address = request.Address;
        society.City = request.City;
        society.State = request.State;
        society.PinCode = request.PinCode;
        society.RegistrationNo = request.RegistrationNo;
        society.BankName = request.BankName;
        society.BankAccountNumber = request.BankAccountNumber;
        society.BankIfsc = request.BankIfsc;
        society.ChequePayeeName = request.ChequePayeeName;
        society.ContactNumber = request.ContactNumber;
        society.InterestOnArrearsPercent = request.InterestOnArrearsPercent;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<SocietyResponse>.Fail("Unable to save society.");
        }

        return ServiceResult<SocietyResponse>.Ok(ToResponse(society));
    }

    public async Task<ServiceResult<AdminUserResponse>> CreateFirstAdminAsync(int societyId, AdminCreateUserRequest request)
    {
        var societyExists = await db.Societies.AnyAsync(s => s.SocietyId == societyId);
        if (!societyExists)
        {
            return ServiceResult<AdminUserResponse>.Fail("Society not found.");
        }

        request.RoleName = RoleNames.Admin;
        return await adminUserService.CreateForSocietyAsync(societyId, request);
    }

    private static SocietyResponse ToResponse(Society society) => new()
    {
        SocietyId = society.SocietyId,
        SocietyName = society.SocietyName ?? string.Empty,
        Address = society.Address,
        City = society.City,
        State = society.State,
        PinCode = society.PinCode,
        RegistrationNo = society.RegistrationNo,
        BankName = society.BankName,
        BankAccountNumber = society.BankAccountNumber,
        BankIfsc = society.BankIfsc,
        ChequePayeeName = society.ChequePayeeName,
        ContactNumber = society.ContactNumber,
        InterestOnArrearsPercent = society.InterestOnArrearsPercent,
        CreatedOn = society.CreatedOn,
    };
}
