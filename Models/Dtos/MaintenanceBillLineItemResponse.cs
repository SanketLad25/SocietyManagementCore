namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceBillLineItemResponse
{
    public int LineItemId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
