using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class KnowledgeArticleRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsPublished { get; set; } = true;
}
