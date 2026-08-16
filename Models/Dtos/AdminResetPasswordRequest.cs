using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class AdminResetPasswordRequest
{
    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
