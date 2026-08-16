namespace SocietyManagementCore.Models.Entities;

/// Insert-only log — deliberately implements neither IAuditable nor ITenantScoped. It has
/// UserId instead of CreatedBy and no ModifiedOn/ModifiedBy at all, so every insert sets
/// SocietyId/CreatedOn explicitly (see AuditLogService) rather than via SaveChangesAsync's
/// auto-stamp loop.
public class AuditLog
{
    public int AuditLogId { get; set; }
    public int SocietyId { get; set; }
    public int? UserId { get; set; }
    public string? Action { get; set; }
    public string? TableName { get; set; }
    public int? RecordId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedOn { get; set; }
}
