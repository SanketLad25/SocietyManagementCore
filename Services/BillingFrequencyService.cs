using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class BillingFrequencyService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<BillingFrequencyResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<BillingFrequencyResponse>();
        }

        var frequencies = await db.BillingFrequencies
            .Where(f => f.SocietyId == societyId)
            .OrderBy(f => f.IntervalMonths)
            .ToListAsync();

        return frequencies.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<BillingFrequencyResponse>> CreateAsync(BillingFrequencyRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<BillingFrequencyResponse>.Fail("No society context for this account.");
        }

        var labelInUse = await db.BillingFrequencies.AnyAsync(f => f.SocietyId == societyId && f.Label == request.Label);
        if (labelInUse)
        {
            return ServiceResult<BillingFrequencyResponse>.Fail("A billing frequency with this label already exists.");
        }

        var frequency = new BillingFrequency
        {
            SocietyId = societyId,
            Label = request.Label,
            IntervalMonths = request.IntervalMonths,
            IsActive = request.IsActive,
        };

        db.BillingFrequencies.Add(frequency);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "BillingFrequency", frequency.BillingFrequencyId, frequency.Label);

        return ServiceResult<BillingFrequencyResponse>.Ok(ToResponse(frequency));
    }

    public async Task<ServiceResult<BillingFrequencyResponse>> UpdateAsync(int billingFrequencyId, BillingFrequencyRequest request)
    {
        var frequency = await FindInSocietyAsync(billingFrequencyId);
        if (frequency is null)
        {
            return ServiceResult<BillingFrequencyResponse>.Fail("Billing frequency not found.");
        }

        var labelInUse = await db.BillingFrequencies.AnyAsync(f =>
            f.SocietyId == frequency.SocietyId && f.Label == request.Label && f.BillingFrequencyId != billingFrequencyId);
        if (labelInUse)
        {
            return ServiceResult<BillingFrequencyResponse>.Fail("A billing frequency with this label already exists.");
        }

        frequency.Label = request.Label;
        frequency.IntervalMonths = request.IntervalMonths;
        frequency.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "BillingFrequency", frequency.BillingFrequencyId, frequency.Label);

        return ServiceResult<BillingFrequencyResponse>.Ok(ToResponse(frequency));
    }

    private Task<BillingFrequency?> FindInSocietyAsync(int billingFrequencyId) =>
        currentUser.SocietyId is { } societyId
            ? db.BillingFrequencies.FirstOrDefaultAsync(f => f.BillingFrequencyId == billingFrequencyId && f.SocietyId == societyId)
            : Task.FromResult<BillingFrequency?>(null);

    private static BillingFrequencyResponse ToResponse(BillingFrequency frequency) => new()
    {
        BillingFrequencyId = frequency.BillingFrequencyId,
        Label = frequency.Label,
        IntervalMonths = frequency.IntervalMonths,
        IsActive = frequency.IsActive,
    };
}
