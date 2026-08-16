using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Data;

public static class DbSeeder
{
    // Bootstrap-only credentials for the very first login — there is no self-service registration
    // and no password-reset flow yet, so this is the sole way in on a fresh database. Rotate it
    // (via a direct UPDATE to UserLogin.PasswordHash, same as committee-role promotion) once a
    // real SuperAdmin session has been established.
    private const string BootstrapSuperAdminUsername = "superadmin";
    private const string BootstrapSuperAdminPassword = "ChangeMe123!";

    public static async Task SeedRolesAsync(SocietyDbContext context)
    {
        var existing = await context.Roles.Select(r => r.RoleName).ToListAsync();
        var missing = RoleNames.All.Except(existing);

        foreach (var roleName in missing)
        {
            context.Roles.Add(new Role { RoleName = roleName });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedSuperAdminAsync(SocietyDbContext context)
    {
        var superAdminExists = await context.UserLogins.AnyAsync(u => u.SocietyId == null);
        if (superAdminExists)
        {
            return;
        }

        var superAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.SuperAdmin);
        if (superAdminRole is null)
        {
            return;
        }

        context.UserLogins.Add(new UserLogin
        {
            SocietyId = null,
            Username = BootstrapSuperAdminUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(BootstrapSuperAdminPassword),
            RoleId = superAdminRole.RoleId,
        });

        await context.SaveChangesAsync();

        Console.WriteLine(
            $"Seeded bootstrap SuperAdmin — username '{BootstrapSuperAdminUsername}', password '{BootstrapSuperAdminPassword}'. " +
            "Leave the Society field blank on the login screen. Rotate this password after first login.");
    }
}
