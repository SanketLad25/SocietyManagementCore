namespace SocietyManagementCore.Models.Dtos;

public class FlatGroupResponse
{
    public int FlatGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<int> FlatIds { get; set; } = new();
}
