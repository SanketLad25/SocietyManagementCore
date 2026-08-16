namespace SocietyManagementCore.Models.Entities;

/// Implemented by entities that get CreatedOn/CreatedBy/ModifiedOn/ModifiedBy auto-stamped
/// by SocietyDbContext.SaveChangesAsync.
public interface IAuditable
{
    DateTime CreatedOn { get; set; }
    int? CreatedBy { get; set; }
    DateTime? ModifiedOn { get; set; }
    int? ModifiedBy { get; set; }
}

/// Implemented by entities whose SocietyId is mandatory and safe to auto-stamp from the
/// current request's tenant context. UserLogin deliberately does NOT implement this — its
/// SocietyId is nullable (NULL = SuperAdmin) and is always set explicitly by the service that
/// creates it, since a SuperAdmin has no ambient SocietyId of its own to fall back on.
public interface ITenantScoped : IAuditable
{
    int SocietyId { get; set; }
}
