using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Data;

/// Backfills default VisitorCategory rows for existing societies on startup — same "backfill for
/// existing tenants" pattern as ComplaintReferenceSeeder/NoticeReferenceSeeder.
///
/// RequiresApprovalDefault/RequiresVehicleNo/RequiresCompanyName defaults below are this seeder's
/// own inferred starting point (not spelled out column-by-column in VisitorManagement.md) — an
/// Admin can retune them per society via VisitorCategoriesController. Delivery/Cab/Courier default
/// to no-approval (auto-check-in, resolved decision #1) since they're routine and short; Guest and
/// Service/Domestic Help default to approval-required since they involve someone staying inside
/// the flat.
public static class VisitorReferenceSeeder
{
    private static readonly (string Name, bool RequiresVehicleNo, bool RequiresCompanyName, bool RequiresApprovalDefault, int DisplayOrder)[] StarterCategories =
    {
        ("Guest", false, false, true, 1),
        ("Delivery", false, true, false, 2),
        ("Cab", true, false, false, 3),
        ("Service/Domestic Help", false, false, true, 4),
        ("Courier", false, true, false, 5),
    };

    public static async Task BackfillExistingSocietiesAsync(SocietyDbContext db)
    {
        var societyIds = await db.Societies.Select(s => s.SocietyId).ToListAsync();
        var societiesWithCategories = await db.VisitorCategories.Select(c => c.SocietyId).Distinct().ToListAsync();
        var pendingSocietyIds = societyIds.Except(societiesWithCategories);

        foreach (var societyId in pendingSocietyIds)
        {
            await SeedSocietyDefaultsAsync(db, societyId);
        }
    }

    public static async Task SeedSocietyDefaultsAsync(SocietyDbContext db, int societyId)
    {
        foreach (var starter in StarterCategories)
        {
            db.VisitorCategories.Add(new VisitorCategory
            {
                SocietyId = societyId,
                CategoryName = starter.Name,
                RequiresVehicleNo = starter.RequiresVehicleNo,
                RequiresCompanyName = starter.RequiresCompanyName,
                RequiresApprovalDefault = starter.RequiresApprovalDefault,
                DisplayOrder = starter.DisplayOrder,
            });
        }

        await db.SaveChangesAsync();
    }
}
