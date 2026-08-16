namespace SocietyManagementCore.Models.Dtos;

public class AmenitySubscriptionResponse
{
    public int SubscriptionId { get; set; }
    public int AmenityId { get; set; }
    public int FlatId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}
