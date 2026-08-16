using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Exemptions only make sense against occupancy-shaped targets (a flat, a group of flats, a
/// wing, or everyone) — not against ParkingType/Amenity, which are already opt-in/assigned
/// separately (mm §2.2).
public class MaintenanceExemptionService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    MaintenanceTargetResolver targetResolver,
    AuditLogService auditLog)
{
    private static readonly string[] AllowedTargetTypeCodes = { "AllFlats", "Wing", "FlatGroup", "SpecificFlat" };

    public async Task<List<MaintenanceExemptionResponse>> ListAsync(int? categoryId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<MaintenanceExemptionResponse>();
        }

        var query = db.MaintenanceExemptions.Where(e => e.SocietyId == societyId);
        if (categoryId is { } cid)
        {
            query = query.Where(e => e.CategoryId == cid);
        }

        var exemptions = await query.OrderByDescending(e => e.EffectiveFrom).ToListAsync();
        var responses = new List<MaintenanceExemptionResponse>();
        foreach (var exemption in exemptions)
        {
            responses.Add(await ToResponseAsync(exemption));
        }

        return responses;
    }

    public async Task<ServiceResult<MaintenanceExemptionResponse>> CreateAsync(MaintenanceExemptionRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail("No society context for this account.");
        }

        var targetTypeCode = await db.ChargeTargetTypes
            .Where(t => t.ChargeTargetTypeId == request.ChargeTargetTypeId)
            .Select(t => t.Code)
            .FirstOrDefaultAsync();

        if (targetTypeCode is null || !AllowedTargetTypeCodes.Contains(targetTypeCode))
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail(
                "Exemptions can only target all flats, a wing, a flat group, or a specific flat.");
        }

        if (!await targetResolver.ValidateTargetAsync(societyId, targetTypeCode, request.TargetId))
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail("The selected target does not exist in your society.");
        }

        var categoryValid = await db.MaintenanceCategories.AnyAsync(c => c.CategoryId == request.CategoryId && c.SocietyId == societyId);
        if (!categoryValid)
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail("Category not found.");
        }

        var exemption = new MaintenanceExemption
        {
            SocietyId = societyId,
            CategoryId = request.CategoryId,
            ChargeTargetTypeId = request.ChargeTargetTypeId,
            TargetId = request.TargetId,
            Reason = request.Reason,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
        };

        db.MaintenanceExemptions.Add(exemption);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "MaintenanceExemption", exemption.ExemptionId);

        return ServiceResult<MaintenanceExemptionResponse>.Ok(await ToResponseAsync(exemption));
    }

    public async Task<ServiceResult<MaintenanceExemptionResponse>> UpdateAsync(int exemptionId, MaintenanceExemptionRequest request)
    {
        var exemption = await FindInSocietyAsync(exemptionId);
        if (exemption is null)
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail("Exemption not found.");
        }

        var targetTypeCode = await db.ChargeTargetTypes
            .Where(t => t.ChargeTargetTypeId == request.ChargeTargetTypeId)
            .Select(t => t.Code)
            .FirstOrDefaultAsync();

        if (targetTypeCode is null || !AllowedTargetTypeCodes.Contains(targetTypeCode))
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail(
                "Exemptions can only target all flats, a wing, a flat group, or a specific flat.");
        }

        if (!await targetResolver.ValidateTargetAsync(exemption.SocietyId, targetTypeCode, request.TargetId))
        {
            return ServiceResult<MaintenanceExemptionResponse>.Fail("The selected target does not exist in your society.");
        }

        exemption.CategoryId = request.CategoryId;
        exemption.ChargeTargetTypeId = request.ChargeTargetTypeId;
        exemption.TargetId = request.TargetId;
        exemption.Reason = request.Reason;
        exemption.EffectiveFrom = request.EffectiveFrom;
        exemption.EffectiveTo = request.EffectiveTo;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "MaintenanceExemption", exemption.ExemptionId);

        return ServiceResult<MaintenanceExemptionResponse>.Ok(await ToResponseAsync(exemption));
    }

    private Task<MaintenanceExemption?> FindInSocietyAsync(int exemptionId) =>
        currentUser.SocietyId is { } societyId
            ? db.MaintenanceExemptions.FirstOrDefaultAsync(e => e.ExemptionId == exemptionId && e.SocietyId == societyId)
            : Task.FromResult<MaintenanceExemption?>(null);

    private async Task<MaintenanceExemptionResponse> ToResponseAsync(MaintenanceExemption exemption)
    {
        var categoryName = await db.MaintenanceCategories.Where(c => c.CategoryId == exemption.CategoryId).Select(c => c.CategoryName).FirstOrDefaultAsync() ?? string.Empty;
        var targetTypeLabel = await db.ChargeTargetTypes.Where(t => t.ChargeTargetTypeId == exemption.ChargeTargetTypeId).Select(t => t.Label).FirstOrDefaultAsync() ?? string.Empty;

        return new MaintenanceExemptionResponse
        {
            ExemptionId = exemption.ExemptionId,
            CategoryId = exemption.CategoryId,
            CategoryName = categoryName,
            ChargeTargetTypeId = exemption.ChargeTargetTypeId,
            ChargeTargetTypeLabel = targetTypeLabel,
            TargetId = exemption.TargetId,
            Reason = exemption.Reason,
            EffectiveFrom = exemption.EffectiveFrom,
            EffectiveTo = exemption.EffectiveTo,
            IsActive = exemption.IsActive,
        };
    }
}
