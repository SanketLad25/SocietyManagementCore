namespace SocietyManagementCore.Models.Dtos;

/// Result of a dry-run — computed, never persisted (mm §6).
public class BillPreviewResponse
{
    public int FlatId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public List<MaintenanceBillLineItemResponse> LineItems { get; set; } = new();
    public decimal TotalAmount { get; set; }
}
