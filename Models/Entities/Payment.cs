namespace SocietyManagementCore.Models.Entities;

/// A Payment row IS the receipt (confirmed decision #1 — no separate Receipt table). ReceiptNo is
/// stamped at insert time in PaymentService.RecordPaymentAsync; there is no payment gateway, so a
/// committee member manually recording this row is itself the confirmation. Never hard-deleted —
/// only cancelled via CancelledReason/CancelledOn/CancelledBy (accounts-management skill §3).
public class Payment : ITenantScoped
{
    public int PaymentId { get; set; }
    public int SocietyId { get; set; }
    public int BillId { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string Status { get; set; } = PaymentStatuses.Confirmed;
    public string? Notes { get; set; }
    public string? CancelledReason { get; set; }
    public DateTime? CancelledOn { get; set; }
    public int? CancelledBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
