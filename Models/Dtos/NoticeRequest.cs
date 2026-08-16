using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class NoticeRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    [Required]
    public string Priority { get; set; } = string.Empty;

    [Required]
    public DateTime PublishDate { get; set; }

    public DateTime? ExpiryDate { get; set; }
}
