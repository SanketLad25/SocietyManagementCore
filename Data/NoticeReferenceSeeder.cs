using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Data;

public static class NoticeReferenceSeeder
{
    private static readonly string[] StarterCategoryNames =
    {
        "General", "Maintenance", "Event", "Emergency", "Meeting", "Water Supply", "Security",
    };

    public static async Task BackfillExistingSocietiesAsync(SocietyDbContext db)
    {
        var societyIds = await db.Societies.Select(s => s.SocietyId).ToListAsync();
        var societiesWithCategories = await db.NoticeCategories.Select(c => c.SocietyId).Distinct().ToListAsync();
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
            db.NoticeCategories.Add(new NoticeCategory { SocietyId = societyId, CategoryName = categoryName });
        }

        await db.SaveChangesAsync();
    }
}
