using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class AuthService(SocietyDbContext db, JwtTokenService jwtTokenService)
{
    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        Society? society = null;
        var societyName = request.SocietyName?.Trim();

        if (!string.IsNullOrEmpty(societyName))
        {
            society = await db.Societies.FirstOrDefaultAsync(s => s.SocietyName == societyName);
            if (society is null)
            {
                return new AuthResult(false, "Invalid society, username or password.", null);
            }
        }

        var userLogin = society is null
            ? await db.UserLogins.FirstOrDefaultAsync(u => u.SocietyId == null && u.Username == request.Username)
            : await db.UserLogins.FirstOrDefaultAsync(u => u.SocietyId == society.SocietyId && u.Username == request.Username);

        if (userLogin is null || !BCrypt.Net.BCrypt.Verify(request.Password, userLogin.PasswordHash))
        {
            return new AuthResult(false, "Invalid society, username or password.", null);
        }

        if (!userLogin.IsActive)
        {
            return new AuthResult(false, "Your account is inactive. Contact the society admin.", null);
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == userLogin.RoleId);
        var roleName = role?.RoleName ?? RoleNames.Resident;

        var fullName = string.Empty;
        var email = userLogin.Username;

        if (userLogin.ResidentId.HasValue)
        {
            var resident = await db.Residents.FindAsync(userLogin.ResidentId.Value);
            if (resident is not null)
            {
                if (resident.IsActive == false)
                {
                    return new AuthResult(false, "Your account is inactive. Contact the society admin.", null);
                }

                fullName = resident.FullName ?? string.Empty;
                email = resident.Email ?? email;
            }
        }

        var (token, expiresAtUtc) = jwtTokenService.GenerateToken(userLogin, roleName);

        return new AuthResult(true, null, new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            UserId = userLogin.UserId,
            FullName = fullName,
            Email = email,
            Role = roleName,
            MustChangePassword = userLogin.MustChangePassword,
            SocietyId = society?.SocietyId,
            SocietyName = society?.SocietyName,
        });
    }

    public async Task<ServiceResult<bool>> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var userLogin = await db.UserLogins.FirstOrDefaultAsync(u => u.UserId == userId);
        if (userLogin is null)
        {
            return ServiceResult<bool>.Fail("User not found.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, userLogin.PasswordHash))
        {
            return ServiceResult<bool>.Fail("Current password is incorrect.");
        }

        userLogin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        userLogin.MustChangePassword = false;
        await db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }
}
