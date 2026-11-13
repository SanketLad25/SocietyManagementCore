namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceBillResponse
{
    public int BillId { get; set; }
    public int FlatId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public int? CycleId { get; set; }
    public int? BillMonth { get; set; }
    public int? BillYear { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal OutstandingAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public List<MaintenanceBillLineItemResponse> LineItems { get; set; } = new();

    // Invoice presentation fields (mm §6 invoice format) — resolved from Flat/Resident/Society at
    // response time so the bill can be rendered as a standalone printable document.
    public decimal? AreaSqFt { get; set; }
    public string ResidentNames { get; set; } = string.Empty;
    public string SocietyName { get; set; } = string.Empty;
    public string? SocietyRegistrationNo { get; set; }
    public string? SocietyAddress { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }
    public string? ChequePayeeName { get; set; }
    public string? ContactNumber { get; set; }
    public decimal? InterestOnArrearsPercent { get; set; }

    // Live-computed in MaintenanceBillingService.ToBillResponseAsync: the sum of this flat's OTHER
    // still-outstanding bills (Unpaid/Partially Paid), excluding this bill's own due amount — see
    // that method for why arrears and this-bill's-own OutstandingAmount are kept deliberately separate.
    public decimal ArrearsAmount { get; set; }
}
