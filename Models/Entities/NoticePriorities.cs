namespace SocietyManagementCore.Models.Entities;

public static class NoticePriorities
{
    public const string Normal = "Normal";
    public const string Important = "Important";
    public const string Urgent = "Urgent";

    public static readonly string[] All = { Normal, Important, Urgent };
}
