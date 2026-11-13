namespace SocietyManagementCore.Models.Dtos;

public class PaymentResponse
{
    public int PaymentId { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public int BillId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CancelledReason { get; set; }
    public DateTime? CancelledOn { get; set; }
}
