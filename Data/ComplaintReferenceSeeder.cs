using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Data;

public static class ComplaintReferenceSeeder
{
    private static readonly string[] StarterCategoryNames =
    {
        "Water", "Electricity", "Security", "Housekeeping", "Parking", "Lift", "Others",
    };

    public static async Task BackfillExistingSocietiesAsync(SocietyDbContext db)
    {
        var societyIds = await db.Societies.Select(s => s.SocietyId).ToListAsync();
        var societiesWithCategories = await db.ComplaintCategories.Select(c => c.SocietyId).Distinct().ToListAsync();
        var pendingSocietyIds = societyIds.Except(societiesWithCategories);

        foreach (var societyId in pendingSocietyIds)
        {
            await SeedSocietyDefaultsAsync(db, societyId);
        }
    }

    public static async Task SeedSocietyDefaultsAsync(SocietyDbContext db, int societyId)
    {
        foreach (var categoryName in StarterCategoryNames)
        {
            db.ComplaintCategories.Add(new ComplaintCategory { SocietyId = societyId, CategoryName = categoryName });
        }

        await db.SaveChangesAsync();
    }
}
