using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class VisitorCategoryService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<VisitorCategoryResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<VisitorCategoryResponse>();
        }

        var categories = await db.VisitorCategories
            .Where(c => c.SocietyId == societyId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return categories.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<VisitorCategoryResponse>> CreateAsync(VisitorCategoryRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<VisitorCategoryResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.VisitorCategories.AnyAsync(c => c.SocietyId == societyId && c.CategoryName == request.CategoryName);
        if (nameInUse)
        {
            return ServiceResult<VisitorCategoryResponse>.Fail("A category with this name already exists.");
        }

        var category = new VisitorCategory
        {
            SocietyId = societyId,
            CategoryName = request.CategoryName,
            RequiresVehicleNo = request.RequiresVehicleNo,
            RequiresCompanyName = request.RequiresCompanyName,
            RequiresApprovalDefault = request.RequiresApprovalDefault,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.VisitorCategories.Add(category);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<VisitorCategoryResponse>.Fail("Unable to save category. Please try again.");
        }

        await auditLog.LogAsync("Create", "VisitorCategory", category.VisitorCategoryId, category.CategoryName);

        return ServiceResult<VisitorCategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<ServiceResult<VisitorCategoryResponse>> UpdateAsync(int categoryId, VisitorCategoryRequest request)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<VisitorCategoryResponse>.Fail("Category not found.");
        }

        var nameInUse = await db.VisitorCategories.AnyAsync(c =>
            c.SocietyId == category.SocietyId && c.CategoryName == request.CategoryName && c.VisitorCategoryId != categoryId);
        if (nameInUse)
        {
            return ServiceResult<VisitorCategoryResponse>.Fail("A category with this name already exists.");
        }

        category.CategoryName = request.CategoryName;
        category.RequiresVehicleNo = request.RequiresVehicleNo;
        category.RequiresCompanyName = request.RequiresCompanyName;
        category.RequiresApprovalDefault = request.RequiresApprovalDefault;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "VisitorCategory", category.VisitorCategoryId, category.CategoryName);

        return ServiceResult<VisitorCategoryResponse>.Ok(ToResponse(category));
    }

    /// Delete outright only while unused; otherwise deactivate instead (mirrors
    /// ComplaintCategoryService/FlatService.DeleteAsync) — a VisitorLog referencing this category
    /// must keep displaying it correctly.
    public async Task<ServiceResult<bool>> DeleteAsync(int categoryId)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<bool>.Fail("Category not found.");
        }

        var inUseCount = await db.VisitorLogs.CountAsync(v => v.VisitorCategoryId == categoryId);
        if (inUseCount > 0)
        {
            return ServiceResult<bool>.Fail(
                $"Cannot delete — {inUseCount} visitor log(s) use this category. Deactivate it instead.");
        }

        db.VisitorCategories.Remove(category);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<bool>.Fail("Cannot delete — this category is still referenced elsewhere. Deactivate it instead.");
        }

        await auditLog.LogAsync("Delete", "VisitorCategory", categoryId, category.CategoryName);

        return ServiceResult<bool>.Ok(true);
    }

    private Task<VisitorCategory?> FindInSocietyAsync(int categoryId) =>
        currentUser.SocietyId is { } societyId
            ? db.VisitorCategories.FirstOrDefaultAsync(c => c.VisitorCategoryId == categoryId && c.SocietyId == societyId)
            : Task.FromResult<VisitorCategory?>(null);

    private static VisitorCategoryResponse ToResponse(VisitorCategory category) => new()
    {
        VisitorCategoryId = category.VisitorCategoryId,
        CategoryName = category.CategoryName,
        RequiresVehicleNo = category.RequiresVehicleNo,
        RequiresCompanyName = category.RequiresCompanyName,
        RequiresApprovalDefault = category.RequiresApprovalDefault,
        DisplayOrder = category.DisplayOrder,
        IsActive = category.IsActive,
    };
}
