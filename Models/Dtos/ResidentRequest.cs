using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ResidentRequest
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, Phone, MaxLength(15)]
    public string Mobile { get; set; } = string.Empty;

    public int? FlatId { get; set; }

    public bool IsOwner { get; set; }

    public bool IsActive { get; set; } = true;
}
