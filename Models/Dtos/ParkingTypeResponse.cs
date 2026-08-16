namespace SocietyManagementCore.Models.Dtos;

public class ParkingTypeResponse
{
    public int ParkingTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
