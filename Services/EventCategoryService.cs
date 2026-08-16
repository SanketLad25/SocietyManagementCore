using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class EventCategoryService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<EventCategoryResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<EventCategoryResponse>();
        }

        var categories = await db.EventCategories
            .Where(c => c.SocietyId == societyId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return categories.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<EventCategoryResponse>> CreateAsync(EventCategoryRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<EventCategoryResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.EventCategories.AnyAsync(c => c.SocietyId == societyId && c.CategoryName == request.CategoryName);
        if (nameInUse)
        {
            return ServiceResult<EventCategoryResponse>.Fail("A category with this name already exists.");
        }

        var category = new EventCategory
        {
            SocietyId = societyId,
            CategoryName = request.CategoryName,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.EventCategories.Add(category);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "EventCategory", category.EventCategoryId, category.CategoryName);

        return ServiceResult<EventCategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<ServiceResult<EventCategoryResponse>> UpdateAsync(int categoryId, EventCategoryRequest request)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<EventCategoryResponse>.Fail("Category not found.");
        }

        var nameInUse = await db.EventCategories.AnyAsync(c =>
            c.SocietyId == category.SocietyId && c.CategoryName == request.CategoryName && c.EventCategoryId != categoryId);
        if (nameInUse)
        {
            return ServiceResult<EventCategoryResponse>.Fail("A category with this name already exists.");
        }

        category.CategoryName = request.CategoryName;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "EventCategory", category.EventCategoryId, category.CategoryName);

        return ServiceResult<EventCategoryResponse>.Ok(ToResponse(category));
    }

    /// Delete outright only while unused; otherwise the caller should deactivate instead — an event
    /// referencing this category must keep displaying it correctly (society-business-rules skill).
    public async Task<ServiceResult<bool>> DeleteAsync(int categoryId)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<bool>.Fail("Category not found.");
        }

        var inUseCount = await db.Events.CountAsync(e => e.EventCategoryId == categoryId);
        if (inUseCount > 0)
        {
            return ServiceResult<bool>.Fail(
                $"Cannot delete — {inUseCount} event(s) use this category. Deactivate it instead.");
        }

        db.EventCategories.Remove(category);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Delete", "EventCategory", categoryId, category.CategoryName);

        return ServiceResult<bool>.Ok(true);
    }

    private Task<EventCategory?> FindInSocietyAsync(int categoryId) =>
        currentUser.SocietyId is { } societyId
            ? db.EventCategories.FirstOrDefaultAsync(c => c.EventCategoryId == categoryId && c.SocietyId == societyId)
            : Task.FromResult<EventCategory?>(null);

    private static EventCategoryResponse ToResponse(EventCategory category) => new()
    {
        EventCategoryId = category.EventCategoryId,
        CategoryName = category.CategoryName,
        DisplayOrder = category.DisplayOrder,
        IsActive = category.IsActive,
    };
}
