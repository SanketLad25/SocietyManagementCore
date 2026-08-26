namespace SocietyManagementCore.Models.Dtos;

public class KnowledgeArticleResponse
{
    public int KnowledgeArticleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
}
