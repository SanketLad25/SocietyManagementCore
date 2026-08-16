namespace SocietyManagementCore.Models.Entities;

public static class EventStatuses
{
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Published, Cancelled };
}
