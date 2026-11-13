using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class PaymentCancelRequest
{
    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;
}
