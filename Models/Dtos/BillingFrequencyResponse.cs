namespace SocietyManagementCore.Models.Dtos;

public class BillingFrequencyResponse
{
    public int BillingFrequencyId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int IntervalMonths { get; set; }
    public bool IsActive { get; set; }
}
