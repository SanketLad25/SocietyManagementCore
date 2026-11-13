namespace SocietyManagementCore.Models.Entities;

/// Payment.PaymentMode is a free-text column — validated against this known set in the service
/// layer (accounts-management skill §4), not with a DB CHECK constraint.
public static class PaymentModes
{
    public const string Cash = "Cash";
    public const string Cheque = "Cheque";
    public const string Upi = "UPI";
    public const string Neft = "NEFT";
    public const string Card = "Card";

    public static readonly string[] All = { Cash, Cheque, Upi, Neft, Card };

    public static bool IsValid(string? mode) => mode is not null && All.Contains(mode);
}
