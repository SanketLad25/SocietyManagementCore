namespace SocietyManagementCore.Models.Dtos;

public class ParkingResponse
{
    public int ParkingId { get; set; }
    public int FlatId { get; set; }
    public string FlatNo { get; set; } = string.Empty;
    public int ParkingTypeId { get; set; }
    public string ParkingTypeName { get; set; } = string.Empty;
    public string? ParkingNo { get; set; }
    public string? VehicleNo { get; set; }
    public string? VehicleType { get; set; }
}
