using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class MaintenanceCategoryService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<MaintenanceCategoryResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<MaintenanceCategoryResponse>();
        }

        var categories = await db.MaintenanceCategories
            .Where(c => c.SocietyId == societyId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return categories.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<MaintenanceCategoryResponse>> CreateAsync(MaintenanceCategoryRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<MaintenanceCategoryResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.MaintenanceCategories.AnyAsync(c => c.SocietyId == societyId && c.CategoryName == request.CategoryName);
        if (nameInUse)
        {
            return ServiceResult<MaintenanceCategoryResponse>.Fail("A category with this name already exists.");
        }

        var category = new MaintenanceCategory
        {
            SocietyId = societyId,
            CategoryName = request.CategoryName,
            CategoryCode = request.CategoryCode,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.MaintenanceCategories.Add(category);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "MaintenanceCategory", category.CategoryId, category.CategoryName);

        return ServiceResult<MaintenanceCategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<ServiceResult<MaintenanceCategoryResponse>> UpdateAsync(int categoryId, MaintenanceCategoryRequest request)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<MaintenanceCategoryResponse>.Fail("Category not found.");
        }

        var nameInUse = await db.MaintenanceCategories.AnyAsync(c =>
            c.SocietyId == category.SocietyId && c.CategoryName == request.CategoryName && c.CategoryId != categoryId);
        if (nameInUse)
        {
            return ServiceResult<MaintenanceCategoryResponse>.Fail("A category with this name already exists.");
        }

        category.CategoryName = request.CategoryName;
        category.CategoryCode = request.CategoryCode;
        category.Description = request.Description;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "MaintenanceCategory", category.CategoryId, category.CategoryName);

        return ServiceResult<MaintenanceCategoryResponse>.Ok(ToResponse(category));
    }

    private Task<MaintenanceCategory?> FindInSocietyAsync(int categoryId) =>
        currentUser.SocietyId is { } societyId
            ? db.MaintenanceCategories.FirstOrDefaultAsync(c => c.CategoryId == categoryId && c.SocietyId == societyId)
            : Task.FromResult<MaintenanceCategory?>(null);

    private static MaintenanceCategoryResponse ToResponse(MaintenanceCategory category) => new()
    {
        CategoryId = category.CategoryId,
        CategoryName = category.CategoryName,
        CategoryCode = category.CategoryCode,
        Description = category.Description,
        DisplayOrder = category.DisplayOrder,
        IsActive = category.IsActive,
    };
}
