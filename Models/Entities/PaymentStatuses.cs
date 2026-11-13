namespace SocietyManagementCore.Models.Entities;

public static class PaymentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Confirmed, Cancelled };
}
