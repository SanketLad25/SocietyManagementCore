using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Society-scoped user management for the Admin role — replaces the old public self-registration
/// flow. Every user created here is stamped to the caller's own society.
public class AdminUserService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task<List<AdminUserResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<AdminUserResponse>();
        }

        var users = await db.UserLogins.Where(u => u.SocietyId == societyId).ToListAsync();
        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleId, r => r.RoleName ?? string.Empty);
        var residentIds = users.Where(u => u.ResidentId.HasValue).Select(u => u.ResidentId!.Value).ToList();
        var residents = await db.Residents
            .Where(r => residentIds.Contains(r.ResidentId))
            .ToDictionaryAsync(r => r.ResidentId, r => r);

        return users.Select(u => ToResponse(u, roles, residents)).ToList();
    }

    public async Task<ServiceResult<AdminUserResponse>> CreateAsync(AdminCreateUserRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<AdminUserResponse>.Fail("No society context for this account.");
        }

        if (!RoleNames.SocietyAssignableRoles.Contains(request.RoleName))
        {
            return ServiceResult<AdminUserResponse>.Fail("That role cannot be assigned here.");
        }

        return await CreateForSocietyAsync(societyId, request);
    }

    /// Used by SocietyService when a SuperAdmin creates the first Admin for a brand-new society
    /// — the target society isn't the caller's own, so it's passed explicitly.
    public async Task<ServiceResult<AdminUserResponse>> CreateForSocietyAsync(int societyId, AdminCreateUserRequest request)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.RoleName);
        if (role is null)
        {
            return ServiceResult<AdminUserResponse>.Fail("Role is not configured.");
        }

        var usernameInUse = await db.UserLogins.AnyAsync(u => u.SocietyId == societyId && u.Username == request.Username);
        if (usernameInUse)
        {
            return ServiceResult<AdminUserResponse>.Fail("A user with this username already exists in this society.");
        }

        Resident? resident = null;
        if (request.RoleName == RoleNames.Resident)
        {
            resident = new Resident
            {
                SocietyId = societyId,
                FullName = request.FullName,
                Email = request.Email,
                Mobile = request.Mobile,
                FlatId = request.FlatId,
                IsOwner = request.IsOwner,
                IsActive = true,
            };
            db.Residents.Add(resident);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return ServiceResult<AdminUserResponse>.Fail("Unable to save resident. Check that the Flat ID exists.");
            }
        }

        var userLogin = new UserLogin
        {
            SocietyId = societyId,
            ResidentId = resident?.ResidentId,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RoleId = role.RoleId,
            IsActive = true,
            // The Admin shares this initial password with the user directly (no email/SMS
            // delivery exists in this codebase) — forcing a change on first login means the
            // Admin never continues to know the account's real password afterward.
            MustChangePassword = true,
        };
        db.UserLogins.Add(userLogin);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<AdminUserResponse>.Fail("Unable to save user login.");
        }

        return ServiceResult<AdminUserResponse>.Ok(ToResponse(userLogin, role.RoleName ?? string.Empty, resident));
    }

    /// Admin-only. Replaces the old role-only update — also edits the resident-profile fields
    /// (when the login is resident-linked) and the login's IsActive flag. Username is not editable.
    public async Task<ServiceResult<AdminUserResponse>> UpdateUserAsync(int userId, AdminUpdateUserRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<AdminUserResponse>.Fail("No society context for this account.");
        }

        if (!RoleNames.SocietyAssignableRoles.Contains(request.RoleName))
        {
            return ServiceResult<AdminUserResponse>.Fail("That role cannot be assigned here.");
        }

        var userLogin = await db.UserLogins.FirstOrDefaultAsync(u => u.UserId == userId && u.SocietyId == societyId);
        if (userLogin is null)
        {
            return ServiceResult<AdminUserResponse>.Fail("User not found.");
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.RoleName);
        if (role is null)
        {
            return ServiceResult<AdminUserResponse>.Fail("Role is not configured.");
        }

        userLogin.RoleId = role.RoleId;
        userLogin.IsActive = request.IsActive;

        Resident? resident = null;
        if (userLogin.ResidentId.HasValue)
        {
            resident = await db.Residents.FindAsync(userLogin.ResidentId.Value);
            if (resident is not null)
            {
                resident.FullName = request.FullName;
                resident.Email = request.Email;
                resident.Mobile = request.Mobile;
            }
        }

        await db.SaveChangesAsync();

        return ServiceResult<AdminUserResponse>.Ok(ToResponse(userLogin, role.RoleName ?? string.Empty, resident));
    }

    /// Admin-only (enforced on the controller). Forces the target user to change the reset
    /// password again on their next login, same as a freshly created account.
    public async Task<ServiceResult<bool>> ResetPasswordAsync(int userId, AdminResetPasswordRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<bool>.Fail("No society context for this account.");
        }

        var userLogin = await db.UserLogins.FirstOrDefaultAsync(u => u.UserId == userId && u.SocietyId == societyId);
        if (userLogin is null)
        {
            return ServiceResult<bool>.Fail("User not found.");
        }

        userLogin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        userLogin.MustChangePassword = true;
        await db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    private static AdminUserResponse ToResponse(UserLogin user, Dictionary<int, string> roles, Dictionary<int, Resident> residents)
    {
        var resident = user.ResidentId.HasValue && residents.TryGetValue(user.ResidentId.Value, out var r) ? r : null;
        var roleName = user.RoleId.HasValue && roles.TryGetValue(user.RoleId.Value, out var name) ? name : string.Empty;
        return ToResponse(user, roleName, resident);
    }

    private static AdminUserResponse ToResponse(UserLogin user, string roleName, Resident? resident)
    {
        return new AdminUserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            RoleName = roleName,
            ResidentId = resident?.ResidentId,
            FullName = resident?.FullName ?? string.Empty,
            Email = resident?.Email,
            Mobile = resident?.Mobile,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
        };
    }
}
