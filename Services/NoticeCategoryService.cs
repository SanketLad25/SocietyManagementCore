using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class NoticeCategoryService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<NoticeCategoryResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<NoticeCategoryResponse>();
        }

        var categories = await db.NoticeCategories
            .Where(c => c.SocietyId == societyId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return categories.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<NoticeCategoryResponse>> CreateAsync(NoticeCategoryRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<NoticeCategoryResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.NoticeCategories.AnyAsync(c => c.SocietyId == societyId && c.CategoryName == request.CategoryName);
        if (nameInUse)
        {
            return ServiceResult<NoticeCategoryResponse>.Fail("A category with this name already exists.");
        }

        var category = new NoticeCategory
        {
            SocietyId = societyId,
            CategoryName = request.CategoryName,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        db.NoticeCategories.Add(category);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "NoticeCategory", category.NoticeCategoryId, category.CategoryName);

        return ServiceResult<NoticeCategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<ServiceResult<NoticeCategoryResponse>> UpdateAsync(int categoryId, NoticeCategoryRequest request)
    {
        var category = await FindInSocietyAsync(categoryId);
        if (category is null)
        {
            return ServiceResult<NoticeCategoryResponse>.Fail("Category not found.");
        }

        var nameInUse = await db.NoticeCategories.AnyAsync(c =>
            c.SocietyId == category.SocietyId && c.CategoryName == request.CategoryName && c.NoticeCategoryId != categoryId);
        if (nameInUse)
        {
            return ServiceResult<NoticeCategoryResponse>.Fail("A category with this name already exists.");
        }

        category.CategoryName = request.CategoryName;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "NoticeCategory", category.NoticeCategoryId, category.CategoryName);

        return ServiceResult<NoticeCategoryResponse>.Ok(ToResponse(category));
    }

    private Task<NoticeCategory?> FindInSocietyAsync(int categoryId) =>
        currentUser.SocietyId is { } societyId
            ? db.NoticeCategories.FirstOrDefaultAsync(c => c.NoticeCategoryId == categoryId && c.SocietyId == societyId)
            : Task.FromResult<NoticeCategory?>(null);

    private static NoticeCategoryResponse ToResponse(NoticeCategory category) => new()
    {
        NoticeCategoryId = category.NoticeCategoryId,
        CategoryName = category.CategoryName,
        DisplayOrder = category.DisplayOrder,
        IsActive = category.IsActive,
    };
}
