using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Data;

public static class EventReferenceSeeder
{
    private static readonly string[] StarterCategoryNames =
    {
        "Festivals", "Sports", "Fitness", "Cultural", "Kids", "Meetings", "Workshops",
    };

    public static async Task BackfillExistingSocietiesAsync(SocietyDbContext db)
    {
        var societyIds = await db.Societies.Select(s => s.SocietyId).ToListAsync();
        var societiesWithCategories = await db.EventCategories.Select(c => c.SocietyId).Distinct().ToListAsync();
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
            db.EventCategories.Add(new EventCategory { SocietyId = societyId, CategoryName = categoryName });
        }

        await db.SaveChangesAsync();
    }
}
