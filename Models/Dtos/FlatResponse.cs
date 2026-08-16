namespace SocietyManagementCore.Models.Dtos;

public class FlatResponse
{
    public int FlatId { get; set; }
    public int? WingId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public int? FloorNo { get; set; }
    public decimal? AreaSqFt { get; set; }
    public decimal? MaintenanceAmount { get; set; }
}
