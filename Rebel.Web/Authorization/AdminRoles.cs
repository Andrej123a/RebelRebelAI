namespace Rebel.Web.Authorization
{
    public static class AdminRoles
    {
        public const string LegacyAdmin = "Admin";
        public const string Manager = "Manager";
        public const string Staff = "Staff";

        public const string Managers = LegacyAdmin + "," + Manager;

        public const string DisplayNameClaim = "display_name";

        public const string StaffMemberIdClaim = "staff_member_id";
    }

    public static class AdminPolicies
    {
        public const string Backstage = "Backstage";
        public const string ManagerOnly = "ManagerOnly";
    }
}
