using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ComplaintCommentRequest
{
    [Required, MaxLength(500)]
    public string CommentText { get; set; } = string.Empty;
}
