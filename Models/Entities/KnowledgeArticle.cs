namespace SocietyManagementCore.Models.Entities;

public class KnowledgeArticle : ITenantScoped
{
    public int KnowledgeArticleId { get; set; }
    public int SocietyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
