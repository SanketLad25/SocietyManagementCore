using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

/// Create-only — there is no update, only record (this) + cancel (PaymentCancelRequest).
public class PaymentRequest
{
    [Required]
    public int BillId { get; set; }

    [Required]
    public decimal AmountPaid { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    /// Validated against PaymentModes.All in PaymentService, not here — a DTO-level [Range]/enum
    /// would have to be kept in sync with that list separately for no benefit.
    [Required]
    public string PaymentMode { get; set; } = string.Empty;

    /// Only meaningful for non-Cash modes (cheque number, UPI/NEFT reference, card auth code) —
    /// optional here since Cash payments have none.
    [MaxLength(100)]
    public string? TransactionId { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}
