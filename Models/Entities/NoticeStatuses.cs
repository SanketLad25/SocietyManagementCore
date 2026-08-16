namespace SocietyManagementCore.Models.Entities;

/// Draft -> Published is one-way; there is no revert (notice.md §1/§4).
public static class NoticeStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
}
