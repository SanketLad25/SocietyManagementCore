namespace SocietyManagementCore.Models.Entities;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Chairman = "Chairman";
    public const string Secretary = "Secretary";
    public const string Treasurer = "Treasurer";
    public const string Resident = "Resident";
    public const string Security = "Security";

    /// Platform-level role, not tied to any society. Creates societies and each society's
    /// first Admin. Deliberately excluded from `All` society-assignable role lists below.
    public const string SuperAdmin = "SuperAdmin";

    public static readonly string[] All =
    {
        Admin, Chairman, Secretary, Treasurer, Resident, Security, SuperAdmin,
    };

    /// Roles an Admin can assign when creating a user within their own society.
    public static readonly string[] SocietyAssignableRoles =
    {
        Admin, Chairman, Secretary, Treasurer, Resident, Security,
    };

    /// Committee roles allowed to manage society data (residents, flats, etc.).
    public const string CommitteeRoles = Admin + "," + Chairman + "," + Secretary + "," + Treasurer;

    /// Narrower than CommitteeRoles — only these two can generate/publish a billing cycle.
    /// Chairman/Secretary can still view cycles and bills (mm §8's view-vs-action split).
    public const string BillingRoles = Admin + "," + Treasurer;

    /// Notice create/edit/publish/delete access (notice.md §8). Narrower than CommitteeRoles —
    /// excludes Treasurer.
    public const string NoticeManagerRoles = Admin + "," + Secretary + "," + Chairman;
}
