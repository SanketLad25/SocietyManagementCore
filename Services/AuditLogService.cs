using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// AuditLog is insert-only and not IAuditable/ITenantScoped (mm §2.5), so every write goes
/// through here rather than SaveChangesAsync's auto-stamp loop.
public class AuditLogService(SocietyDbContext db, ICurrentUserContext currentUser)
{
    public async Task LogAsync(string action, string tableName, int? recordId, string? details = null)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            SocietyId = societyId,
            UserId = currentUser.UserId,
            Action = action,
            TableName = tableName,
            RecordId = recordId,
            Details = details,
            CreatedOn = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }
}
