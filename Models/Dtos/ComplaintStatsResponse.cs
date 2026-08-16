namespace SocietyManagementCore.Models.Dtos;

public class ComplaintStatsResponse
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByCategory { get; set; } = new();
    public Dictionary<string, int> ByPriority { get; set; } = new();
}
