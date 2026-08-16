using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class ComplaintCategoryService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<ComplaintCategoryResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<ComplaintCategoryResponse>();
        }

        var categories = await db.ComplaintCategories
            .Where(c => c.SocietyId == societyId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return categories.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<ComplaintCategoryResponse>> CreateAsync(ComplaintCategoryRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<ComplaintCategoryResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.ComplaintCategories.AnyAsync(c => c.SocietyId == societyId && c.CategoryName == request.CategoryName);
        if (nameInUse)
        {
            return ServiceResult<ComplaintCategoryResponse>.Fail("A category with this name already exists.");
        }

        var category = new ComplaintCategory
        {
            SocietyId = societyId,
            CategoryName = request.CategoryName,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.ComplaintCategories.Add(category);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "ComplaintCategory", category.ComplaintCategoryId, category.CategoryName);

        return ServiceResult<ComplaintCategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<ServiceResult<ComplaintCategoryResponse>> UpdateAsync(int categoryId, ComplaintCategoryRequest request)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<ComplaintCategoryResponse>.Fail("Category not found.");
        }

        var nameInUse = await db.ComplaintCategories.AnyAsync(c =>
            c.SocietyId == category.SocietyId && c.CategoryName == request.CategoryName && c.ComplaintCategoryId != categoryId);
        if (nameInUse)
        {
            return ServiceResult<ComplaintCategoryResponse>.Fail("A category with this name already exists.");
        }

        category.CategoryName = request.CategoryName;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "ComplaintCategory", category.ComplaintCategoryId, category.CategoryName);

        return ServiceResult<ComplaintCategoryResponse>.Ok(ToResponse(category));
    }

    /// Delete outright only while unused; otherwise the caller should deactivate instead
    /// (complaints.md §7) — a complaint referencing this category must keep displaying it correctly.
    public async Task<ServiceResult<bool>> DeleteAsync(int categoryId)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<bool>.Fail("Category not found.");
        }

        var inUseCount = await db.Complaints.CountAsync(c => c.CategoryId == categoryId);
        if (inUseCount > 0)
        {
            return ServiceResult<bool>.Fail(
                $"Cannot delete — {inUseCount} complaint(s) use this category. Deactivate it instead.");
        }

        db.ComplaintCategories.Remove(category);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Delete", "ComplaintCategory", categoryId, category.CategoryName);

        return ServiceResult<bool>.Ok(true);
    }

    private Task<ComplaintCategory?> FindInSocietyAsync(int categoryId) =>
        currentUser.SocietyId is { } societyId
            ? db.ComplaintCategories.FirstOrDefaultAsync(c => c.ComplaintCategoryId == categoryId && c.SocietyId == societyId)
            : Task.FromResult<ComplaintCategory?>(null);

    private static ComplaintCategoryResponse ToResponse(ComplaintCategory category) => new()
    {
        ComplaintCategoryId = category.ComplaintCategoryId,
        CategoryName = category.CategoryName,
        DisplayOrder = category.DisplayOrder,
        IsActive = category.IsActive,
    };
}
