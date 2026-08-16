namespace SocietyManagementCore.Models.Entities;

public class Notice : ITenantScoped
{
    public int NoticeId { get; set; }
    public int SocietyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Priority { get; set; } = NoticePriorities.Normal;
    public string Status { get; set; } = NoticeStatuses.Draft;
    public DateTime PublishDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
