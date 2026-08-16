using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class ChargeTargetTypeService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<ChargeTargetTypeResponse>> ListAsync()
    {
        var targetTypes = await db.ChargeTargetTypes.Where(t => t.IsActive).OrderBy(t => t.Label).ToListAsync();

        if (currentUser.SocietyId is not { } societyId)
        {
            return targetTypes.Select(t => new ChargeTargetTypeResponse
            {
                ChargeTargetTypeId = t.ChargeTargetTypeId,
                Code = t.Code,
                Label = t.Label,
                IsEnabledForSociety = false,
            }).ToList();
        }

        var enabledIds = await db.SocietyChargeTargetTypes
            .Where(s => s.SocietyId == societyId && s.IsEnabled)
            .Select(s => s.ChargeTargetTypeId)
            .ToListAsync();

        return targetTypes.Select(t => new ChargeTargetTypeResponse
        {
            ChargeTargetTypeId = t.ChargeTargetTypeId,
            Code = t.Code,
            Label = t.Label,
            IsEnabledForSociety = enabledIds.Contains(t.ChargeTargetTypeId),
        }).ToList();
    }

    public async Task<ServiceResult<bool>> SetEnablementAsync(int chargeTargetTypeId, bool isEnabled)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<bool>.Fail("No society context for this account.");
        }

        var mapping = await db.SocietyChargeTargetTypes
            .FirstOrDefaultAsync(s => s.SocietyId == societyId && s.ChargeTargetTypeId == chargeTargetTypeId);

        if (mapping is null)
        {
            var targetTypeExists = await db.ChargeTargetTypes.AnyAsync(t => t.ChargeTargetTypeId == chargeTargetTypeId);
            if (!targetTypeExists)
            {
                return ServiceResult<bool>.Fail("Charge target type not found.");
            }

            mapping = new SocietyChargeTargetType
            {
                SocietyId = societyId,
                ChargeTargetTypeId = chargeTargetTypeId,
                IsEnabled = isEnabled,
            };
            db.SocietyChargeTargetTypes.Add(mapping);
        }
        else
        {
            mapping.IsEnabled = isEnabled;
        }

        await db.SaveChangesAsync();
        await auditLog.LogAsync("SetEnablement", "SocietyChargeTargetType", chargeTargetTypeId, $"IsEnabled={isEnabled}");

        return ServiceResult<bool>.Ok(true);
    }
}
