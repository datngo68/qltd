using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;

namespace QuanLyAnTrua.ViewComponents
{
    public class UserAvatarViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public UserAvatarViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = SessionHelper.GetUserId(HttpContext);
            if (userId == null)
            {
                return Content(string.Empty);
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user == null)
            {
                return Content(string.Empty);
            }

            var avatarUrl = AvatarHelper.GetAvatarUrl(user.AvatarPath);
            ViewBag.AvatarUrl = avatarUrl;
            ViewBag.FullName = user.Name;

            return View();
        }
    }
}

