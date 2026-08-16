using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class FlatGroupMemberRequest
{
    [Required]
    public int FlatId { get; set; }
}
