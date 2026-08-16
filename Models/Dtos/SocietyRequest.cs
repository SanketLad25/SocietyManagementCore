using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class SocietyRequest
{
    [Required, MaxLength(100)]
    public string SocietyName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(10)]
    public string? PinCode { get; set; }

    [MaxLength(50)]
    public string? RegistrationNo { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }

    [MaxLength(30)]
    public string? BankAccountNumber { get; set; }

    [MaxLength(20)]
    public string? BankIfsc { get; set; }

    [MaxLength(150)]
    public string? ChequePayeeName { get; set; }

    [MaxLength(20)]
    public string? ContactNumber { get; set; }

    [Range(0, 100)]
    public decimal? InterestOnArrearsPercent { get; set; }
}
