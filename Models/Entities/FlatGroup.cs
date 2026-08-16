namespace SocietyManagementCore.Models.Entities;

public class FlatGroup : ITenantScoped
{
    public int FlatGroupId { get; set; }
    public int SocietyId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
