namespace SocietyManagementCore.Models.Dtos;

public class ComplaintUpdateResponse
{
    public int ComplaintUpdateId { get; set; }
    public string UpdateType { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? CommentText { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}
