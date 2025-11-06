using Microsoft.AspNetCore.Http;

namespace QuanLyAnTrua.Helpers
{
    public static class SessionHelper
    {
        public static int? GetUserId(HttpContext context)
        {
            return context.Session.GetInt32("UserId");
        }

        public static string? GetRole(HttpContext context)
        {
            return context.Session.GetString("Role");
        }

        public static int? GetGroupId(HttpContext context)
        {
            return context.Session.GetInt32("GroupId");
        }

        public static bool IsSuperAdmin(HttpContext context)
        {
            return GetRole(context) == "SuperAdmin";
        }

        public static bool IsAdmin(HttpContext context)
        {
            var role = GetRole(context);
            return role == "Admin" || role == "SuperAdmin";
        }

        public static bool IsUser(HttpContext context)
        {
            return GetRole(context) == "User";
        }
    }
}

