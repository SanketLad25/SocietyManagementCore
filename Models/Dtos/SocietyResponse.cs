namespace SocietyManagementCore.Models.Dtos;

public class SocietyResponse
{
    public int SocietyId { get; set; }
    public string SocietyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PinCode { get; set; }
    public string? RegistrationNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }
    public string? ChequePayeeName { get; set; }
    public string? ContactNumber { get; set; }
    public decimal? InterestOnArrearsPercent { get; set; }
    public DateTime CreatedOn { get; set; }
}
