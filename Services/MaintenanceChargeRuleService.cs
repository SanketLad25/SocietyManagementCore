using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services.Calculation;

namespace SocietyManagementCore.Services;

public class MaintenanceChargeRuleService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    MaintenanceTargetResolver targetResolver,
    CalculationStrategyFactory strategyFactory,
    AuditLogService auditLog)
{
    public async Task<List<MaintenanceChargeRuleResponse>> ListAsync(int? categoryId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<MaintenanceChargeRuleResponse>();
        }

        var query = db.MaintenanceChargeRules.Where(r => r.SocietyId == societyId);
        if (categoryId is { } cid)
        {
            query = query.Where(r => r.CategoryId == cid);
        }

        var rules = await query.OrderByDescending(r => r.EffectiveFrom).ToListAsync();
        var responses = new List<MaintenanceChargeRuleResponse>();
        foreach (var rule in rules)
        {
            responses.Add(await ToResponseAsync(rule));
        }

        return responses;
    }

    public async Task<ServiceResult<MaintenanceChargeRuleResponse>> CreateAsync(MaintenanceChargeRuleRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("No society context for this account.");
        }

        var targetTypeCode = await db.ChargeTargetTypes
            .Where(t => t.ChargeTargetTypeId == request.ChargeTargetTypeId)
            .Select(t => t.Code)
            .FirstOrDefaultAsync();

        if (targetTypeCode is null)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("Charge target type not found.");
        }

        if (!await targetResolver.ValidateTargetAsync(societyId, targetTypeCode, request.TargetId))
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail(
                "The selected target does not exist in your society, or is required for this target type.");
        }

        var categoryValid = await db.MaintenanceCategories.AnyAsync(c => c.CategoryId == request.CategoryId && c.SocietyId == societyId);
        if (!categoryValid)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("Category not found.");
        }

        var frequencyValid = await db.BillingFrequencies.AnyAsync(f => f.BillingFrequencyId == request.BillingFrequencyId && f.SocietyId == societyId);
        if (!frequencyValid)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("Billing frequency not found.");
        }

        var method = await db.CalculationMethods.FirstOrDefaultAsync(m => m.CalculationMethodId == request.CalculationMethodId);
        if (method is null)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("Calculation method not found.");
        }

        var (parametersValid, parametersError) = strategyFactory.Resolve(method.MethodCode).ValidateParameters(request.ParametersJson);
        if (!parametersValid)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail(parametersError ?? "ParametersJson is invalid for the selected calculation method.");
        }

        var hasOverlap = await HasOverlapAsync(societyId, request.CategoryId, request.ChargeTargetTypeId, request.TargetId, request.EffectiveFrom);
        if (hasOverlap)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail(
                "An active rule already covers this category and target from this date. Revise the existing rule instead of creating a new one.");
        }

        var rule = new MaintenanceChargeRule
        {
            SocietyId = societyId,
            CategoryId = request.CategoryId,
            CalculationMethodId = request.CalculationMethodId,
            ChargeTargetTypeId = request.ChargeTargetTypeId,
            TargetId = request.TargetId,
            Rate = request.Rate,
            ParametersJson = request.ParametersJson,
            BillingFrequencyId = request.BillingFrequencyId,
            EffectiveFrom = request.EffectiveFrom,
        };

        db.MaintenanceChargeRules.Add(rule);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "MaintenanceChargeRule", rule.RuleId);

        return ServiceResult<MaintenanceChargeRuleResponse>.Ok(await ToResponseAsync(rule));
    }

    /// Never edits Rate in place — closes the current rule's EffectiveTo the day before the new
    /// rate starts, then inserts a new versioned row, so bills generated against the old rate
    /// stay computed correctly (mm §2.2).
    public async Task<ServiceResult<MaintenanceChargeRuleResponse>> ReviseAsync(int ruleId, ReviseChargeRuleRequest request)
    {
        var currentRule = await FindInSocietyAsync(ruleId);
        if (currentRule is null)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("Charge rule not found.");
        }

        if (request.EffectiveFrom <= currentRule.EffectiveFrom)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("The new rate's effective date must be after the current rule's start date.");
        }

        var methodCode = await db.CalculationMethods
            .Where(m => m.CalculationMethodId == currentRule.CalculationMethodId)
            .Select(m => m.MethodCode)
            .FirstOrDefaultAsync();
        if (methodCode is null)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail("Calculation method not found.");
        }

        var (parametersValid, parametersError) = strategyFactory.Resolve(methodCode).ValidateParameters(request.ParametersJson);
        if (!parametersValid)
        {
            return ServiceResult<MaintenanceChargeRuleResponse>.Fail(parametersError ?? "ParametersJson is invalid for the selected calculation method.");
        }

        var newRule = new MaintenanceChargeRule
        {
            SocietyId = currentRule.SocietyId,
            CategoryId = currentRule.CategoryId,
            CalculationMethodId = currentRule.CalculationMethodId,
            ChargeTargetTypeId = currentRule.ChargeTargetTypeId,
            TargetId = currentRule.TargetId,
            Rate = request.Rate,
            ParametersJson = request.ParametersJson,
            BillingFrequencyId = currentRule.BillingFrequencyId,
            EffectiveFrom = request.EffectiveFrom,
        };

        currentRule.EffectiveTo = request.EffectiveFrom.AddDays(-1);

        db.MaintenanceChargeRules.Add(newRule);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Revise", "MaintenanceChargeRule", newRule.RuleId, $"Superseded RuleId={currentRule.RuleId}");

        return ServiceResult<MaintenanceChargeRuleResponse>.Ok(await ToResponseAsync(newRule));
    }

    private async Task<bool> HasOverlapAsync(int societyId, int categoryId, int chargeTargetTypeId, int? targetId, DateOnly effectiveFrom)
    {
        return await db.MaintenanceChargeRules.AnyAsync(r =>
            r.SocietyId == societyId &&
            r.CategoryId == categoryId &&
            r.ChargeTargetTypeId == chargeTargetTypeId &&
            r.TargetId == targetId &&
            r.IsActive &&
            (r.EffectiveTo == null || r.EffectiveTo >= effectiveFrom));
    }

    private Task<MaintenanceChargeRule?> FindInSocietyAsync(int ruleId) =>
        currentUser.SocietyId is { } societyId
            ? db.MaintenanceChargeRules.FirstOrDefaultAsync(r => r.RuleId == ruleId && r.SocietyId == societyId)
            : Task.FromResult<MaintenanceChargeRule?>(null);

    private async Task<MaintenanceChargeRuleResponse> ToResponseAsync(MaintenanceChargeRule rule)
    {
        var categoryName = await db.MaintenanceCategories.Where(c => c.CategoryId == rule.CategoryId).Select(c => c.CategoryName).FirstOrDefaultAsync() ?? string.Empty;
        var methodName = await db.CalculationMethods.Where(m => m.CalculationMethodId == rule.CalculationMethodId).Select(m => m.MethodName).FirstOrDefaultAsync() ?? string.Empty;
        var targetType = await db.ChargeTargetTypes.Where(t => t.ChargeTargetTypeId == rule.ChargeTargetTypeId).FirstOrDefaultAsync();
        var frequencyLabel = await db.BillingFrequencies.Where(f => f.BillingFrequencyId == rule.BillingFrequencyId).Select(f => f.Label).FirstOrDefaultAsync() ?? string.Empty;

        return new MaintenanceChargeRuleResponse
        {
            RuleId = rule.RuleId,
            CategoryId = rule.CategoryId,
            CategoryName = categoryName,
            CalculationMethodId = rule.CalculationMethodId,
            CalculationMethodName = methodName,
            ChargeTargetTypeId = rule.ChargeTargetTypeId,
            ChargeTargetTypeLabel = targetType?.Label ?? string.Empty,
            TargetId = rule.TargetId,
            TargetLabel = await ResolveTargetLabelAsync(targetType?.Code, rule.TargetId),
            Rate = rule.Rate,
            ParametersJson = rule.ParametersJson,
            BillingFrequencyId = rule.BillingFrequencyId,
            BillingFrequencyLabel = frequencyLabel,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo,
            IsActive = rule.IsActive,
        };
    }

    private async Task<string?> ResolveTargetLabelAsync(string? targetTypeCode, int? targetId)
    {
        if (targetTypeCode is null || targetId is not { } id)
        {
            return null;
        }

        return targetTypeCode switch
        {
            "Wing" => $"Wing #{id}",
            "FlatGroup" => await db.FlatGroups.Where(g => g.FlatGroupId == id).Select(g => g.GroupName).FirstOrDefaultAsync(),
            "SpecificFlat" => await db.Flats.Where(f => f.FlatId == id).Select(f => f.FlatNo).FirstOrDefaultAsync(),
            "ParkingType" => await db.ParkingTypes.Where(p => p.ParkingTypeId == id).Select(p => p.TypeName).FirstOrDefaultAsync(),
            "Amenity" => await db.Amenities.Where(a => a.AmenityId == id).Select(a => a.AmenityName).FirstOrDefaultAsync(),
            _ => null,
        };
    }

    /// Resident-facing "My Charges" (mm §10) — resolves what applies to the caller's own flat,
    /// in plain language, not a raw rule dump.
    public async Task<List<MyChargeResponse>> GetMyChargesAsync()
    {
        if (currentUser.SocietyId is not { } societyId || currentUser.UserId is not { } userId)
        {
            return new List<MyChargeResponse>();
        }

        var flatId = await db.UserLogins
            .Where(u => u.UserId == userId)
            .Join(db.Residents, u => u.ResidentId, r => r.ResidentId, (u, r) => r.FlatId)
            .FirstOrDefaultAsync();

        if (flatId is not { } myFlatId)
        {
            return new List<MyChargeResponse>();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeRules = await db.MaintenanceChargeRules
            .Where(r => r.SocietyId == societyId && r.IsActive && r.EffectiveFrom <= today && (r.EffectiveTo == null || r.EffectiveTo >= today))
            .ToListAsync();

        var activeExemptions = await db.MaintenanceExemptions
            .Where(e => e.SocietyId == societyId && e.IsActive && e.EffectiveFrom <= today && (e.EffectiveTo == null || e.EffectiveTo >= today))
            .ToListAsync();

        var results = new List<MyChargeResponse>();
        foreach (var rule in activeRules)
        {
            var targetTypeCode = await db.ChargeTargetTypes.Where(t => t.ChargeTargetTypeId == rule.ChargeTargetTypeId).Select(t => t.Code).FirstOrDefaultAsync();
            if (targetTypeCode is null)
            {
                continue;
            }

            var flatIds = await targetResolver.ResolveFlatIdsAsync(societyId, targetTypeCode, rule.TargetId);
            if (!flatIds.Contains(myFlatId))
            {
                continue;
            }

            var isExempt = false;
            foreach (var exemption in activeExemptions.Where(e => e.CategoryId == rule.CategoryId))
            {
                var exemptTypeCode = await db.ChargeTargetTypes.Where(t => t.ChargeTargetTypeId == exemption.ChargeTargetTypeId).Select(t => t.Code).FirstOrDefaultAsync();
                if (exemptTypeCode is null)
                {
                    continue;
                }

                var exemptFlatIds = await targetResolver.ResolveFlatIdsAsync(societyId, exemptTypeCode, exemption.TargetId);
                if (exemptFlatIds.Contains(myFlatId))
                {
                    isExempt = true;
                    break;
                }
            }

            if (isExempt)
            {
                continue;
            }

            var categoryName = await db.MaintenanceCategories.Where(c => c.CategoryId == rule.CategoryId).Select(c => c.CategoryName).FirstOrDefaultAsync() ?? string.Empty;
            var methodName = await db.CalculationMethods.Where(m => m.CalculationMethodId == rule.CalculationMethodId).Select(m => m.MethodName).FirstOrDefaultAsync() ?? string.Empty;
            var frequencyLabel = await db.BillingFrequencies.Where(f => f.BillingFrequencyId == rule.BillingFrequencyId).Select(f => f.Label).FirstOrDefaultAsync() ?? string.Empty;

            results.Add(new MyChargeResponse
            {
                CategoryName = categoryName,
                Rate = rule.Rate,
                CalculationMethodName = methodName,
                BillingFrequencyLabel = frequencyLabel,
                EffectiveFrom = rule.EffectiveFrom,
            });
        }

        return results;
    }
}
