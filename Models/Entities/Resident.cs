namespace SocietyManagementCore.Models.Entities;

public class Resident : ITenantScoped
{
    public int ResidentId { get; set; }
    public int SocietyId { get; set; }
    public int? FlatId { get; set; }
    public string? FullName { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public bool? IsOwner { get; set; }
    public bool? IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
