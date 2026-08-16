using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Data;

public static class MaintenanceReferenceSeeder
{
    private static readonly (string Code, string Name)[] CalculationMethods =
    {
        ("FixedAmount", "Fixed amount"),
        ("PerSqFt", "Per square foot"),
        ("PerUnit", "Per unit (e.g. per parking slot)"),
        ("SlabTiered", "Slab / tiered"),
        ("PercentageOfCategory", "Percentage of another category"),
        ("Formula", "Custom formula"),
    };

    private static readonly (string Code, string Label)[] ChargeTargetTypes =
    {
        ("AllFlats", "All flats"),
        ("Wing", "A wing"),
        ("FlatGroup", "A group of flats"),
        ("SpecificFlat", "A specific flat"),
        ("ParkingType", "A parking type"),
        ("Amenity", "An amenity"),
    };

    private static readonly string[] StarterCategoryNames =
    {
        "Water Charges", "Sinking Fund", "Repair Fund", "Education Fund", "Service Charges",
        "Insurance Charges", "Common Electricity Charges", "Common Welfare Charges",
        "Fire Audit Charges", "Parking Maintenance Charges", "Amenities Charges",
    };

    private static readonly (string Label, int IntervalMonths)[] StarterBillingFrequencies =
    {
        ("Monthly", 1), ("Quarterly", 3), ("Half-Yearly", 6), ("Annually", 12), ("One-Time", 0),
    };

    public static async Task SeedGlobalReferenceDataAsync(SocietyDbContext db)
    {
        var existingMethodCodes = await db.CalculationMethods.Select(m => m.MethodCode).ToListAsync();
        foreach (var (code, name) in CalculationMethods)
        {
            if (!existingMethodCodes.Contains(code))
            {
                db.CalculationMethods.Add(new CalculationMethod { MethodCode = code, MethodName = name });
            }
        }

        var existingTargetCodes = await db.ChargeTargetTypes.Select(t => t.Code).ToListAsync();
        foreach (var (code, label) in ChargeTargetTypes)
        {
            if (!existingTargetCodes.Contains(code))
            {
                db.ChargeTargetTypes.Add(new ChargeTargetType { Code = code, Label = label });
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
        }
    }

    public static async Task BackfillExistingSocietiesAsync(SocietyDbContext db)
    {
        var societyIds = await db.Societies.Select(s => s.SocietyId).ToListAsync();
        var societiesWithCategories = await db.MaintenanceCategories.Select(c => c.SocietyId).Distinct().ToListAsync();
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
            db.MaintenanceCategories.Add(new MaintenanceCategory { SocietyId = societyId, CategoryName = categoryName });
        }

        foreach (var (label, intervalMonths) in StarterBillingFrequencies)
        {
            db.BillingFrequencies.Add(new BillingFrequency { SocietyId = societyId, Label = label, IntervalMonths = intervalMonths });
        }

        var chargeTargetTypeIds = await db.ChargeTargetTypes.Select(t => t.ChargeTargetTypeId).ToListAsync();
        foreach (var chargeTargetTypeId in chargeTargetTypeIds)
        {
            db.SocietyChargeTargetTypes.Add(new SocietyChargeTargetType
            {
                SocietyId = societyId,
                ChargeTargetTypeId = chargeTargetTypeId,
                IsEnabled = true,
            });
        }

        await db.SaveChangesAsync();
    }
}
