using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services.Calculation;

namespace SocietyManagementCore.Services;

/// The generation engine (mm §6): resolves every active MaintenanceChargeRule against every flat
/// via MaintenanceTargetResolver, subtracts exemption coverage, computes amounts via
/// CalculationStrategyFactory, and snapshots the result into MaintenanceBillLineItem rows.
public class MaintenanceBillingService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    MaintenanceTargetResolver targetResolver,
    CalculationStrategyFactory strategyFactory,
    AuditLogService auditLog)
{
    public async Task<List<MaintenanceBillingCycleResponse>> ListCyclesAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<MaintenanceBillingCycleResponse>();
        }

        var cycles = await db.MaintenanceBillingCycles
            .Where(c => c.SocietyId == societyId)
            .OrderByDescending(c => c.PeriodStart)
            .ToListAsync();

        return cycles.Select(ToCycleResponse).ToList();
    }

    public async Task<ServiceResult<MaintenanceBillingCycleResponse>> CreateCycleAsync(MaintenanceBillingCycleRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<MaintenanceBillingCycleResponse>.Fail("No society context for this account.");
        }

        if (request.PeriodEnd < request.PeriodStart)
        {
            return ServiceResult<MaintenanceBillingCycleResponse>.Fail("Period end must be on or after period start.");
        }

        var overlaps = await db.MaintenanceBillingCycles.AnyAsync(c =>
            c.SocietyId == societyId && request.PeriodStart <= c.PeriodEnd && request.PeriodEnd >= c.PeriodStart);
        if (overlaps)
        {
            return ServiceResult<MaintenanceBillingCycleResponse>.Fail("A billing cycle already covers part of this period.");
        }

        var cycle = new MaintenanceBillingCycle
        {
            SocietyId = societyId,
            CycleLabel = request.CycleLabel,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            DueDate = request.DueDate,
            Status = "Draft",
        };

        db.MaintenanceBillingCycles.Add(cycle);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "MaintenanceBillingCycle", cycle.CycleId);

        return ServiceResult<MaintenanceBillingCycleResponse>.Ok(ToCycleResponse(cycle));
    }

    /// Computes what one flat would be billed for this cycle without saving anything (mm §6).
    public async Task<ServiceResult<BillPreviewResponse>> PreviewAsync(int cycleId, int flatId)
    {
        var cycle = await FindCycleInSocietyAsync(cycleId);
        if (cycle is null)
        {
            return ServiceResult<BillPreviewResponse>.Fail("Billing cycle not found.");
        }

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == flatId && f.SocietyId == cycle.SocietyId);
        if (flat is null)
        {
            return ServiceResult<BillPreviewResponse>.Fail("Flat not found.");
        }

        var (activeRules, activeExemptions, targetTypeCodes, methodCodes, categories) = await LoadBillingInputsAsync(cycle);
        var lineItems = await ComputeLineItemsForFlatAsync(cycle.SocietyId, flat, activeRules, activeExemptions, targetTypeCodes, methodCodes, categories);

        return ServiceResult<BillPreviewResponse>.Ok(new BillPreviewResponse
        {
            FlatId = flat.FlatId,
            FlatNo = flat.FlatNo ?? string.Empty,
            LineItems = lineItems.Select(li => new MaintenanceBillLineItemResponse
            {
                CategoryId = li.Rule.CategoryId,
                CategoryName = li.Category.CategoryName,
                Description = li.Category.CategoryName,
                Amount = li.Amount,
            }).ToList(),
            TotalAmount = lineItems.Sum(li => li.Amount),
        });
    }

    /// Refuses to run twice on the same cycle (mm §6 step 4) — once Generated, revisit via a new
    /// cycle rather than mutating this one.
    ///
    /// Bug fix (code review, 2026-08-26): the original version checked `cycle.Status != "Draft"`
    /// once, then looped every flat with a SaveChangesAsync per bill and no transaction — two
    /// concurrent calls (double-click, or two committee members) could both read "Draft" before
    /// either wrote "Generated", both proceed through the whole flat loop, and both insert a full
    /// set of bills with no error, no signal, silently double-billing every flat. Fixed two ways:
    /// (1) the Draft -> Generating transition below is a single conditional UPDATE
    /// (ExecuteUpdateAsync WHERE Status = 'Draft'), not read-then-write — a losing concurrent call
    /// gets rowsUpdated == 0 and fails cleanly instead of racing through; (2) a unique index on
    /// MaintenanceBill(CycleId, FlatId) (016_maintenance_billing_race_fix.sql) is a second line of
    /// defense against any other double-insert path. As a side effect this also makes a retry
    /// after a partial failure (e.g. one flat's rule throws) safe: already-billed flats are
    /// skipped via the same unique constraint rather than re-billed, and the cycle reverts to
    /// "Draft" (not left stuck in "Generating") if generation doesn't fully complete, so a retry
    /// is actually possible.
    public async Task<ServiceResult<List<MaintenanceBillResponse>>> GenerateAsync(int cycleId)
    {
        var cycle = await FindCycleInSocietyAsync(cycleId);
        if (cycle is null)
        {
            return ServiceResult<List<MaintenanceBillResponse>>.Fail("Billing cycle not found.");
        }

        if (cycle.Status != "Draft")
        {
            return ServiceResult<List<MaintenanceBillResponse>>.Fail("This cycle has already been generated.");
        }

        var claimed = await db.MaintenanceBillingCycles
            .Where(c => c.CycleId == cycle.CycleId && c.Status == "Draft")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, "Generating"));
        if (claimed == 0)
        {
            // Lost a race against a concurrent Generate call on the same cycle — the winner's
            // conditional UPDATE already claimed it.
            return ServiceResult<List<MaintenanceBillResponse>>.Fail("This cycle has already been generated.");
        }

        var (activeRules, activeExemptions, targetTypeCodes, methodCodes, categories) = await LoadBillingInputsAsync(cycle);
        var flats = await db.Flats.Where(f => f.SocietyId == cycle.SocietyId).ToListAsync();
        var alreadyBilledFlatIds = await db.MaintenanceBills
            .Where(b => b.CycleId == cycle.CycleId && b.FlatId != null)
            .Select(b => b.FlatId!.Value)
            .ToListAsync();
        var alreadyBilled = new HashSet<int>(alreadyBilledFlatIds);

        var responses = new List<MaintenanceBillResponse>();

        try
        {
            foreach (var flat in flats)
            {
                // A retry after a previous partial failure — flats already billed in the earlier
                // attempt are skipped rather than double-billed.
                if (alreadyBilled.Contains(flat.FlatId))
                {
                    continue;
                }

                var lineItems = await ComputeLineItemsForFlatAsync(cycle.SocietyId, flat, activeRules, activeExemptions, targetTypeCodes, methodCodes, categories);
                if (lineItems.Count == 0)
                {
                    continue;
                }

                var totalAmount = lineItems.Sum(li => li.Amount);

                // Late-fee auto-computation isn't built yet (accounts-management skill §2) — always 0
                // for now. OutstandingAmount deliberately already folds this in (not just totalAmount)
                // so the invariant "OutstandingAmount always includes PenaltyAmount" holds from bill
                // generation onward, matching PaymentService.RecomputeBillStatusAsync's amountDue
                // formula exactly. When a late-fee feature lands, compute it here instead of hardcoding
                // — OutstandingAmount below stays correct automatically, no other call site to update.
                const decimal penaltyAmount = 0m;

                var bill = new MaintenanceBill
                {
                    SocietyId = cycle.SocietyId,
                    FlatId = flat.FlatId,
                    BillMonth = cycle.PeriodStart.Month,
                    BillYear = cycle.PeriodStart.Year,
                    Amount = totalAmount,
                    DueDate = cycle.DueDate,
                    Status = "Unpaid",
                    CycleId = cycle.CycleId,
                    OutstandingAmount = totalAmount + penaltyAmount,
                    PenaltyAmount = penaltyAmount,
                };

                db.MaintenanceBills.Add(bill);
                await db.SaveChangesAsync();

                foreach (var lineItem in lineItems)
                {
                    db.MaintenanceBillLineItems.Add(new MaintenanceBillLineItem
                    {
                        SocietyId = cycle.SocietyId,
                        BillId = bill.BillId,
                        RuleId = lineItem.Rule.RuleId,
                        CategoryId = lineItem.Rule.CategoryId,
                        Description = lineItem.Category.CategoryName,
                        Amount = lineItem.Amount,
                        CreatedOn = DateTime.UtcNow,
                    });
                }

                await db.SaveChangesAsync();
                responses.Add(await ToBillResponseAsync(bill));
            }
        }
        catch (DbUpdateException)
        {
            await RevertToDraftAsync(cycle.CycleId);
            return ServiceResult<List<MaintenanceBillResponse>>.Fail(
                "Unable to save one or more bills (a duplicate or database error occurred). The cycle was reverted to Draft — fix the underlying issue and try generating again; flats already billed won't be billed twice.");
        }
        catch (Exception)
        {
            // A calculation-strategy failure (e.g. malformed charge-rule parameters) — don't leave
            // the cycle stuck in "Generating" with no way to retry.
            await RevertToDraftAsync(cycle.CycleId);
            return ServiceResult<List<MaintenanceBillResponse>>.Fail(
                "Unable to compute charges for one or more flats — check the charge rules for this society. The cycle was reverted to Draft; flats already billed won't be billed twice when you retry.");
        }

        cycle.Status = "Generated";
        cycle.GeneratedOn = DateTime.UtcNow;
        cycle.GeneratedBy = currentUser.UserId;
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Generate", "MaintenanceBillingCycle", cycle.CycleId, $"{responses.Count} bills generated");

        return ServiceResult<List<MaintenanceBillResponse>>.Ok(responses);
    }

    private async Task RevertToDraftAsync(int cycleId)
    {
        await db.MaintenanceBillingCycles
            .Where(c => c.CycleId == cycleId && c.Status == "Generating")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, "Draft"));
    }

    /// Flips a Generated cycle to Published — the point at which residents can see their bills
    /// (mm §6). One-way, same as Generate.
    public async Task<ServiceResult<bool>> PublishAsync(int cycleId)
    {
        var cycle = await FindCycleInSocietyAsync(cycleId);
        if (cycle is null)
        {
            return ServiceResult<bool>.Fail("Billing cycle not found.");
        }

        if (cycle.Status != "Generated")
        {
            return ServiceResult<bool>.Fail("Only a generated cycle can be published.");
        }

        cycle.Status = "Published";
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Publish", "MaintenanceBillingCycle", cycle.CycleId);

        return ServiceResult<bool>.Ok(true);
    }

    /// A Resident's flatId is always resolved server-side from their own session — never trusted
    /// from a query-string parameter (mm §5) — and they only ever see bills from a Published cycle.
    public async Task<List<MaintenanceBillResponse>> ListBillsAsync(int? flatId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<MaintenanceBillResponse>();
        }

        var query = db.MaintenanceBills.Where(b => b.SocietyId == societyId);

        if (currentUser.RoleName == RoleNames.Resident)
        {
            var myFlatId = await ResolveMyFlatIdAsync();
            if (myFlatId is not { } resolvedFlatId)
            {
                return new List<MaintenanceBillResponse>();
            }

            query = query.Where(b => b.FlatId == resolvedFlatId &&
                db.MaintenanceBillingCycles.Any(c => c.CycleId == b.CycleId && c.Status == "Published"));
        }
        else if (flatId is { } requestedFlatId)
        {
            query = query.Where(b => b.FlatId == requestedFlatId);
        }

        var bills = await query.OrderByDescending(b => b.BillId).ToListAsync();
        var responses = new List<MaintenanceBillResponse>();
        foreach (var bill in bills)
        {
            responses.Add(await ToBillResponseAsync(bill));
        }

        return responses;
    }

    public async Task<MaintenanceBillResponse?> GetBillAsync(int billId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return null;
        }

        var bill = await db.MaintenanceBills.FirstOrDefaultAsync(b => b.BillId == billId && b.SocietyId == societyId);
        if (bill is null)
        {
            return null;
        }

        if (currentUser.RoleName == RoleNames.Resident)
        {
            var myFlatId = await ResolveMyFlatIdAsync();
            if (myFlatId is null || bill.FlatId != myFlatId)
            {
                return null;
            }

            var cyclePublished = await db.MaintenanceBillingCycles.AnyAsync(c => c.CycleId == bill.CycleId && c.Status == "Published");
            if (!cyclePublished)
            {
                return null;
            }
        }

        return await ToBillResponseAsync(bill);
    }

    private async Task<int?> ResolveMyFlatIdAsync()
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await db.UserLogins
            .Where(u => u.UserId == userId)
            .Join(db.Residents, u => u.ResidentId, r => r.ResidentId, (u, r) => r.FlatId)
            .FirstOrDefaultAsync();
    }

    private async Task<(
        List<MaintenanceChargeRule> ActiveRules,
        List<MaintenanceExemption> ActiveExemptions,
        Dictionary<int, string> TargetTypeCodes,
        Dictionary<int, string> MethodCodes,
        Dictionary<int, MaintenanceCategory> Categories)> LoadBillingInputsAsync(MaintenanceBillingCycle cycle)
    {
        // The rate/exemption in effect at the START of the period governs the whole cycle — a
        // rate change mid-period is a Phase-3-scope simplification, not prorated.
        var activeRules = await db.MaintenanceChargeRules.Where(r =>
            r.SocietyId == cycle.SocietyId && r.IsActive &&
            r.EffectiveFrom <= cycle.PeriodStart && (r.EffectiveTo == null || r.EffectiveTo >= cycle.PeriodStart)).ToListAsync();

        var activeExemptions = await db.MaintenanceExemptions.Where(e =>
            e.SocietyId == cycle.SocietyId && e.IsActive &&
            e.EffectiveFrom <= cycle.PeriodStart && (e.EffectiveTo == null || e.EffectiveTo >= cycle.PeriodStart)).ToListAsync();

        var targetTypeCodes = await db.ChargeTargetTypes.ToDictionaryAsync(t => t.ChargeTargetTypeId, t => t.Code);
        var methodCodes = await db.CalculationMethods.ToDictionaryAsync(m => m.CalculationMethodId, m => m.MethodCode);
        var categories = await db.MaintenanceCategories.Where(c => c.SocietyId == cycle.SocietyId).ToDictionaryAsync(c => c.CategoryId, c => c);

        return (activeRules, activeExemptions, targetTypeCodes, methodCodes, categories);
    }

    private async Task<List<(MaintenanceChargeRule Rule, MaintenanceCategory Category, decimal Amount)>> ComputeLineItemsForFlatAsync(
        int societyId,
        Flat flat,
        List<MaintenanceChargeRule> activeRules,
        List<MaintenanceExemption> activeExemptions,
        Dictionary<int, string> targetTypeCodes,
        Dictionary<int, string> methodCodes,
        Dictionary<int, MaintenanceCategory> categories)
    {
        var flatParkingCounts = await db.Parkings
            .Where(p => p.FlatId == flat.FlatId && p.ParkingTypeId != null)
            .GroupBy(p => p.ParkingTypeId!.Value)
            .Select(g => new { ParkingTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParkingTypeId, x => x.Count);

        var applicableRules = new List<MaintenanceChargeRule>();
        foreach (var rule in activeRules)
        {
            if (!targetTypeCodes.TryGetValue(rule.ChargeTargetTypeId, out var targetCode))
            {
                continue;
            }

            var flatIds = await targetResolver.ResolveFlatIdsAsync(societyId, targetCode, rule.TargetId);
            if (!flatIds.Contains(flat.FlatId))
            {
                continue;
            }

            var isExempt = false;
            foreach (var exemption in activeExemptions.Where(e => e.CategoryId == rule.CategoryId))
            {
                if (!targetTypeCodes.TryGetValue(exemption.ChargeTargetTypeId, out var exemptCode))
                {
                    continue;
                }

                var exemptFlatIds = await targetResolver.ResolveFlatIdsAsync(societyId, exemptCode, exemption.TargetId);
                if (exemptFlatIds.Contains(flat.FlatId))
                {
                    isExempt = true;
                    break;
                }
            }

            if (!isExempt)
            {
                applicableRules.Add(rule);
            }
        }

        // PercentageOfCategory rules resolve last — they read an amount already computed for a
        // different category on this same flat (mm §3), so every other rule must run first.
        var ordered = applicableRules.OrderBy(r => methodCodes.GetValueOrDefault(r.CalculationMethodId) == "PercentageOfCategory" ? 1 : 0);

        var categoryAmounts = new Dictionary<int, decimal>();
        var results = new List<(MaintenanceChargeRule, MaintenanceCategory, decimal)>();

        foreach (var rule in ordered)
        {
            var methodCode = methodCodes.GetValueOrDefault(rule.CalculationMethodId);
            if (methodCode is null || !categories.TryGetValue(rule.CategoryId, out var category))
            {
                continue;
            }

            var strategy = strategyFactory.Resolve(methodCode);
            var context = new FlatBillingContext
            {
                Flat = flat,
                UnitCountResolver = parkingTypeId => flatParkingCounts.GetValueOrDefault(parkingTypeId, 0),
                CategoryAmountResolver = categoryId => categoryAmounts.GetValueOrDefault(categoryId, 0m),
            };

            var amount = strategy.Calculate(rule, context);
            categoryAmounts[rule.CategoryId] = categoryAmounts.GetValueOrDefault(rule.CategoryId, 0m) + amount;
            results.Add((rule, category, amount));
        }

        return results;
    }

    private Task<MaintenanceBillingCycle?> FindCycleInSocietyAsync(int cycleId) =>
        currentUser.SocietyId is { } societyId
            ? db.MaintenanceBillingCycles.FirstOrDefaultAsync(c => c.CycleId == cycleId && c.SocietyId == societyId)
            : Task.FromResult<MaintenanceBillingCycle?>(null);

    private async Task<MaintenanceBillResponse> ToBillResponseAsync(MaintenanceBill bill)
    {
        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == bill.FlatId);
        var society = await db.Societies.FirstOrDefaultAsync(s => s.SocietyId == bill.SocietyId);
        var residentNames = await db.Residents
            .Where(r => r.FlatId == bill.FlatId && r.IsActive == true)
            .OrderByDescending(r => r.IsOwner)
            .Select(r => r.FullName)
            .ToListAsync();
        var lineItems = await (
            from li in db.MaintenanceBillLineItems
            join c in db.MaintenanceCategories on li.CategoryId equals c.CategoryId
            where li.BillId == bill.BillId
            select new MaintenanceBillLineItemResponse
            {
                LineItemId = li.LineItemId,
                CategoryId = li.CategoryId,
                CategoryName = c.CategoryName,
                Description = li.Description,
                Amount = li.Amount,
            }).ToListAsync();

        // Arrears = this flat's OTHER still-outstanding bills, not this bill's own due amount
        // (PaymentService.RecomputeBillStatusAsync keeps those two numbers deliberately separate —
        // see that method's comment — so summing them here, not there, is the one place arrears is
        // computed, live, off each other bill's own OutstandingAmount).
        var arrearsAmount = await db.MaintenanceBills
            .Where(b => b.SocietyId == bill.SocietyId && b.FlatId == bill.FlatId && b.BillId != bill.BillId &&
                (b.Status == "Unpaid" || b.Status == "Partially Paid"))
            .SumAsync(b => (decimal?)(b.OutstandingAmount ?? 0m)) ?? 0m;

        return new MaintenanceBillResponse
        {
            BillId = bill.BillId,
            FlatId = bill.FlatId ?? 0,
            FlatNo = flat?.FlatNo ?? string.Empty,
            CycleId = bill.CycleId,
            BillMonth = bill.BillMonth,
            BillYear = bill.BillYear,
            Amount = bill.Amount ?? 0,
            DueDate = bill.DueDate,
            Status = bill.Status ?? string.Empty,
            OutstandingAmount = bill.OutstandingAmount ?? 0,
            PenaltyAmount = bill.PenaltyAmount ?? 0,
            LineItems = lineItems,
            AreaSqFt = flat?.AreaSqFt,
            ResidentNames = string.Join(" & ", residentNames.Where(n => !string.IsNullOrWhiteSpace(n))),
            SocietyName = society?.SocietyName ?? string.Empty,
            SocietyRegistrationNo = society?.RegistrationNo,
            SocietyAddress = FormatSocietyAddress(society),
            BankName = society?.BankName,
            BankAccountNumber = society?.BankAccountNumber,
            BankIfsc = society?.BankIfsc,
            ChequePayeeName = society?.ChequePayeeName,
            ContactNumber = society?.ContactNumber,
            InterestOnArrearsPercent = society?.InterestOnArrearsPercent,
            ArrearsAmount = arrearsAmount,
        };
    }

    private static string? FormatSocietyAddress(Society? society)
    {
        if (society is null)
        {
            return null;
        }

        var parts = new[] { society.Address, society.City, society.State, society.PinCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var formatted = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(formatted) ? null : formatted;
    }

    private static MaintenanceBillingCycleResponse ToCycleResponse(MaintenanceBillingCycle cycle) => new()
    {
        CycleId = cycle.CycleId,
        CycleLabel = cycle.CycleLabel,
        PeriodStart = cycle.PeriodStart,
        PeriodEnd = cycle.PeriodEnd,
        DueDate = cycle.DueDate,
        Status = cycle.Status,
        GeneratedOn = cycle.GeneratedOn,
    };
}
