namespace SocietyManagementCore.Models.Entities;

public class Parking : ITenantScoped
{
    public int ParkingId { get; set; }
    public int SocietyId { get; set; }
    public int? FlatId { get; set; }
    public int? ParkingTypeId { get; set; }
    public string? ParkingNo { get; set; }
    public string? VehicleNo { get; set; }
    public string? VehicleType { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
