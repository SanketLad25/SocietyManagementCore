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

    /// Who can read MaintenanceBill data (code review, 2026-08-26): CommitteeRoles see every
    /// flat's bills; Resident is included because MaintenanceBillingService scopes a Resident
    /// caller down to their own flat's bills server-side. Deliberately excludes Security — a bare
    /// [Authorize] previously let any authenticated non-Resident role (including Security) hit
    /// this endpoint and fall into the "see everything" branch, leaking every resident's
    /// maintenance amounts/outstanding balances/penalties to a role that has no business reason
    /// to see them.
    public const string MaintenanceBillViewRoles = CommitteeRoles + "," + Resident;

    /// Notice create/edit/publish/delete access (notice.md §8). Narrower than CommitteeRoles —
    /// excludes Treasurer.
    public const string NoticeManagerRoles = Admin + "," + Secretary + "," + Chairman;

    /// Every role except SuperAdmin — who is allowed to use the chat assistant (SocChatBot.md
    /// "Roles"). Same membership as SocietyAssignableRoles, duplicated as an independent const
    /// string rather than derived from that array: [Authorize(Roles = ...)] requires a compile-time
    /// constant, and SocietyAssignableRoles is a string[] used elsewhere via .Contains(...), never in
    /// an attribute. SuperAdmin is excluded because its JWT carries no SocietyId — there is no
    /// society context for the assistant to answer about.
    public const string ChattableRoles = Admin + "," + Chairman + "," + Secretary + "," + Treasurer + "," + Resident + "," + Security;

    /// Who can run the visitor gate (log/check-in/check-out/cancel a visitor entry). Narrower
    /// than CommitteeRoles — Chairman/Secretary/Treasurer have no operational reason to run the
    /// gate; Admin stays as a fallback since there's no self-service path to create a Security
    /// account (manual DB promotion only).
    public const string VisitorGateRoles = Admin + "," + Security;
}
