using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Helpers
{
    /// <summary>
    /// Helper class để xử lý các logic query filtering chung được sử dụng trong nhiều controllers
    /// </summary>
    public static class QueryFilterHelper
    {
        /// <summary>
        /// Filter query theo GroupId dựa vào quyền của user hiện tại
        /// - SuperAdmin: có thể filter theo groupId parameter, hoặc xem tất cả nếu không có groupId
        /// - Admin/User: chỉ xem dữ liệu của group mình (lấy từ session)
        /// </summary>
        /// <typeparam name="T">Entity type phải có property GroupId nullable</typeparam>
        /// <param name="query">Query cần filter</param>
        /// <param name="httpContext">HttpContext để lấy thông tin session</param>
        /// <param name="filterGroupId">GroupId để filter (chỉ áp dụng cho SuperAdmin)</param>
        /// <returns>Query đã được filter</returns>
        public static IQueryable<T> FilterByGroup<T>(IQueryable<T> query, HttpContext httpContext, int? filterGroupId = null) where T : class
        {
            if (SessionHelper.IsSuperAdmin(httpContext))
            {
                // SuperAdmin có thể filter theo nhóm
                if (filterGroupId.HasValue)
                {
                    // Sử dụng reflection để truy cập property GroupId
                    var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
                    var property = System.Linq.Expressions.Expression.Property(parameter, "GroupId");
                    var constant = System.Linq.Expressions.Expression.Constant(filterGroupId.Value, typeof(int));
                    var equals = System.Linq.Expressions.Expression.Equal(property, constant);
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(equals, parameter);
                    query = query.Where(lambda);
                }
                // Nếu không chọn nhóm thì hiển thị tất cả
            }
            else
            {
                // Admin/User chỉ xem dữ liệu của group mình
                var currentGroupId = SessionHelper.GetGroupId(httpContext);
                if (currentGroupId.HasValue)
                {
                    var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
                    var property = System.Linq.Expressions.Expression.Property(parameter, "GroupId");
                    var constant = System.Linq.Expressions.Expression.Constant(currentGroupId.Value, typeof(int));
                    var equals = System.Linq.Expressions.Expression.Equal(property, constant);
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(equals, parameter);
                    query = query.Where(lambda);
                }
                else
                {
                    // User không có group, không thấy gì
                    query = query.Where(e => false);
                }
            }

            return query;
        }

        /// <summary>
        /// Load danh sách groups cho dropdown trong ViewBag
        /// - SuperAdmin: xem tất cả groups active
        /// - Admin: chỉ xem group của mình
        /// - User: không xem groups
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="httpContext">HttpContext để lấy thông tin session</param>
        /// <returns>Danh sách groups hoặc null nếu không có quyền</returns>
        public static async Task<List<Group>?> LoadGroupsForDropdownAsync(ApplicationDbContext context, HttpContext httpContext)
        {
            if (SessionHelper.IsSuperAdmin(httpContext))
            {
                // SuperAdmin xem tất cả groups
                return await context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }
            else if (SessionHelper.IsAdmin(httpContext))
            {
                // Admin chỉ xem nhóm của mình
                var groupId = SessionHelper.GetGroupId(httpContext);
                if (groupId.HasValue)
                {
                    return await context.Groups
                        .Where(g => g.Id == groupId.Value && g.IsActive)
                        .OrderBy(g => g.Name)
                        .ToListAsync();
                }
            }

            // User thường không xem groups
            return null;
        }

        /// <summary>
        /// Filter user query theo GroupId và quyền của user hiện tại
        /// - SuperAdmin: có thể filter theo groupId parameter, hoặc xem tất cả nếu không có groupId
        /// - Admin: chỉ xem users trong group của mình
        /// - User: chỉ xem chính mình (nếu không có group)
        /// </summary>
        /// <param name="query">User query cần filter</param>
        /// <param name="httpContext">HttpContext để lấy thông tin session</param>
        /// <param name="filterGroupId">GroupId để filter (chỉ áp dụng cho SuperAdmin)</param>
        /// <param name="activeOnly">Chỉ lấy users active</param>
        /// <returns>Query đã được filter</returns>
        public static IQueryable<User> FilterUsersByGroup(IQueryable<User> query, HttpContext httpContext, int? filterGroupId = null, bool activeOnly = false)
        {
            if (activeOnly)
            {
                query = query.Where(u => u.IsActive);
            }

            if (SessionHelper.IsSuperAdmin(httpContext))
            {
                // SuperAdmin có thể filter theo nhóm
                if (filterGroupId.HasValue)
                {
                    query = query.Where(u => u.GroupId == filterGroupId.Value);
                }
                // Nếu không chọn nhóm thì hiển thị tất cả
            }
            else
            {
                var currentGroupId = SessionHelper.GetGroupId(httpContext);
                if (currentGroupId.HasValue)
                {
                    query = query.Where(u => u.GroupId == currentGroupId.Value);
                }
                else
                {
                    // User thường có thể không có GroupId, cho phép chọn bản thân
                    if (SessionHelper.IsUser(httpContext))
                    {
                        var currentUserId = SessionHelper.GetUserId(httpContext);
                        if (currentUserId.HasValue)
                        {
                            query = query.Where(u => u.Id == currentUserId.Value);
                        }
                        else
                        {
                            query = query.Where(u => false);
                        }
                    }
                    else
                    {
                        query = query.Where(u => false);
                    }
                }
            }

            return query;
        }

        /// <summary>
        /// Kiểm tra xem user hiện tại có quyền truy cập entity với GroupId cụ thể không
        /// </summary>
        /// <param name="httpContext">HttpContext để lấy thông tin session</param>
        /// <param name="entityGroupId">GroupId của entity cần kiểm tra</param>
        /// <returns>True nếu có quyền truy cập</returns>
        public static bool CanAccessGroup(HttpContext httpContext, int? entityGroupId)
        {
            if (SessionHelper.IsSuperAdmin(httpContext))
            {
                return true;
            }

            var currentGroupId = SessionHelper.GetGroupId(httpContext);
            if (!currentGroupId.HasValue || !entityGroupId.HasValue)
            {
                return false;
            }

            return currentGroupId.Value == entityGroupId.Value;
        }
    }
}
