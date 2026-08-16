namespace SocietyManagementCore.Models.Dtos;

public class ChargeTargetTypeResponse
{
    public int ChargeTargetTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsEnabledForSociety { get; set; }
}
