using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class FlatGroupService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<FlatGroupResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<FlatGroupResponse>();
        }

        var groups = await db.FlatGroups
            .Where(g => g.SocietyId == societyId)
            .OrderBy(g => g.GroupName)
            .ToListAsync();

        var memberMap = await db.FlatGroupMembers
            .Where(m => m.SocietyId == societyId)
            .GroupBy(m => m.FlatGroupId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(m => m.FlatId).ToList());

        return groups.Select(g => ToResponse(g, memberMap.GetValueOrDefault(g.FlatGroupId) ?? new List<int>())).ToList();
    }

    public async Task<ServiceResult<FlatGroupResponse>> CreateAsync(FlatGroupRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<FlatGroupResponse>.Fail("No society context for this account.");
        }

        var nameInUse = await db.FlatGroups.AnyAsync(g => g.SocietyId == societyId && g.GroupName == request.GroupName);
        if (nameInUse)
        {
            return ServiceResult<FlatGroupResponse>.Fail("A flat group with this name already exists.");
        }

        var group = new FlatGroup
        {
            SocietyId = societyId,
            GroupName = request.GroupName,
            Description = request.Description,
            IsActive = request.IsActive,
        };

        db.FlatGroups.Add(group);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "FlatGroup", group.FlatGroupId, group.GroupName);

        return ServiceResult<FlatGroupResponse>.Ok(ToResponse(group, new List<int>()));
    }

    public async Task<ServiceResult<FlatGroupResponse>> UpdateAsync(int flatGroupId, FlatGroupRequest request)
    {
        var group = await FindInSocietyAsync(flatGroupId);
        if (group is null)
        {
            return ServiceResult<FlatGroupResponse>.Fail("Flat group not found.");
        }

        var nameInUse = await db.FlatGroups.AnyAsync(g =>
            g.SocietyId == group.SocietyId && g.GroupName == request.GroupName && g.FlatGroupId != flatGroupId);
        if (nameInUse)
        {
            return ServiceResult<FlatGroupResponse>.Fail("A flat group with this name already exists.");
        }

        group.GroupName = request.GroupName;
        group.Description = request.Description;
        group.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "FlatGroup", group.FlatGroupId, group.GroupName);

        var flatIds = await db.FlatGroupMembers.Where(m => m.FlatGroupId == flatGroupId).Select(m => m.FlatId).ToListAsync();
        return ServiceResult<FlatGroupResponse>.Ok(ToResponse(group, flatIds));
    }

    public async Task<ServiceResult<bool>> AddMemberAsync(int flatGroupId, FlatGroupMemberRequest request)
    {
        var group = await FindInSocietyAsync(flatGroupId);
        if (group is null)
        {
            return ServiceResult<bool>.Fail("Flat group not found.");
        }

        var flatInSociety = await db.Flats.AnyAsync(f => f.FlatId == request.FlatId && f.SocietyId == group.SocietyId);
        if (!flatInSociety)
        {
            return ServiceResult<bool>.Fail("Flat not found in this society.");
        }

        var alreadyMember = await db.FlatGroupMembers.AnyAsync(m => m.FlatGroupId == flatGroupId && m.FlatId == request.FlatId);
        if (alreadyMember)
        {
            return ServiceResult<bool>.Fail("This flat is already in the group.");
        }

        db.FlatGroupMembers.Add(new FlatGroupMember
        {
            FlatGroupId = flatGroupId,
            FlatId = request.FlatId,
            SocietyId = group.SocietyId,
        });

        await db.SaveChangesAsync();
        await auditLog.LogAsync("AddMember", "FlatGroupMember", flatGroupId, $"FlatId={request.FlatId}");

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> RemoveMemberAsync(int flatGroupId, int flatId)
    {
        var group = await FindInSocietyAsync(flatGroupId);
        if (group is null)
        {
            return ServiceResult<bool>.Fail("Flat group not found.");
        }

        var member = await db.FlatGroupMembers.FirstOrDefaultAsync(m => m.FlatGroupId == flatGroupId && m.FlatId == flatId);
        if (member is null)
        {
            return ServiceResult<bool>.Fail("This flat is not in the group.");
        }

        db.FlatGroupMembers.Remove(member);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("RemoveMember", "FlatGroupMember", flatGroupId, $"FlatId={flatId}");

        return ServiceResult<bool>.Ok(true);
    }

    private Task<FlatGroup?> FindInSocietyAsync(int flatGroupId) =>
        currentUser.SocietyId is { } societyId
            ? db.FlatGroups.FirstOrDefaultAsync(g => g.FlatGroupId == flatGroupId && g.SocietyId == societyId)
            : Task.FromResult<FlatGroup?>(null);

    private static FlatGroupResponse ToResponse(FlatGroup group, List<int> flatIds) => new()
    {
        FlatGroupId = group.FlatGroupId,
        GroupName = group.GroupName,
        Description = group.Description,
        IsActive = group.IsActive,
        FlatIds = flatIds,
    };
}
