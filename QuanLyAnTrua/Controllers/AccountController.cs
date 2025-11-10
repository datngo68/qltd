using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using System.ComponentModel.DataAnnotations;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Account/Login (no authorize for login)
        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            // If already logged in, redirect to home
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Kiểm tra remember me cookie
            var rememberMeUserId = Request.Cookies["RememberMeUserId"];
            if (!string.IsNullOrEmpty(rememberMeUserId) && int.TryParse(rememberMeUserId, out int userId))
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user != null)
                {
                    // Tự động đăng nhập lại
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("Username", user.Username ?? "");
                    HttpContext.Session.SetString("FullName", user.Name);
                    HttpContext.Session.SetString("Role", user.Role);
                    if (user.GroupId.HasValue)
                    {
                        HttpContext.Session.SetInt32("GroupId", user.GroupId.Value);
                    }
                    else
                    {
                        HttpContext.Session.Remove("GroupId");
                    }

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // User không tồn tại hoặc không active, xóa cookies
                    Response.Cookies.Delete("RememberMeToken");
                    Response.Cookies.Delete("RememberMeUserId");
                }
            }

            return View();
        }

        // POST: Account/Login (no authorize for login)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập tên đăng nhập và mật khẩu");
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng");
                return View();
            }

            if (PasswordHelper.VerifyPassword(password, user.PasswordHash))
            {
                // Set session
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username ?? "");
                HttpContext.Session.SetString("FullName", user.Name);
                HttpContext.Session.SetString("Role", user.Role);
                if (user.GroupId.HasValue)
                {
                    HttpContext.Session.SetInt32("GroupId", user.GroupId.Value);
                }
                else
                {
                    HttpContext.Session.Remove("GroupId");
                }

                // Nếu chọn "Remember me", lưu thông tin vào cookie
                if (rememberMe)
                {
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.Now.AddDays(30) // 30 ngày
                    };

                    // Tạo token đơn giản (có thể dùng cách bảo mật hơn)
                    var rememberToken = TokenHelper.GenerateSecureToken(32);
                    Response.Cookies.Append("RememberMeToken", rememberToken, cookieOptions);
                    Response.Cookies.Append("RememberMeUserId", user.Id.ToString(), cookieOptions);
                }
                else
                {
                    // Xóa remember me cookies nếu không chọn
                    Response.Cookies.Delete("RememberMeToken");
                    Response.Cookies.Delete("RememberMeUserId");
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng");
            return View();
        }

        // POST: Account/Logout
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            // Xóa remember me cookies
            Response.Cookies.Delete("RememberMeToken");
            Response.Cookies.Delete("RememberMeUserId");

            return RedirectToAction("Login");
        }

        // GET: Account/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            // Load danh sách ngân hàng
            ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();

            return View(user);
        }

        // POST: Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(int id, [Bind("Id,Name,BankName,BankAccount,AccountHolderName,TelegramUserId")] User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != id)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _context.Users.FindAsync(id);
                    if (existingUser == null)
                    {
                        return NotFound();
                    }

                    // Không cho phép đổi username - giữ nguyên username hiện tại
                    // Cập nhật thông tin người dùng
                    existingUser.Name = user.Name;

                    // Cập nhật thông tin tài khoản ngân hàng
                    existingUser.BankName = user.BankName;
                    existingUser.BankAccount = user.BankAccount;
                    existingUser.AccountHolderName = user.AccountHolderName;
                    
                    // Cập nhật Telegram User ID
                    existingUser.TelegramUserId = user.TelegramUserId;

                    _context.Update(existingUser);
                    await _context.SaveChangesAsync();

                    // Update session
                    HttpContext.Session.SetString("FullName", existingUser.Name);

                    TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
                    return RedirectToAction(nameof(Profile));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Nếu ModelState không hợp lệ, cần load lại user để hiển thị đúng
            var userToView = await _context.Users.FindAsync(id);
            if (userToView == null)
            {
                return NotFound();
            }

            // Giữ lại các giá trị đã nhập
            userToView.Name = user.Name;
            userToView.BankName = user.BankName;
            userToView.BankAccount = user.BankAccount;
            userToView.AccountHolderName = user.AccountHolderName;
            userToView.TelegramUserId = user.TelegramUserId;

            // Load danh sách ngân hàng
            ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();

            return View(userToView);
        }

        // GET: Account/ChangePassword
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            // Validate
            if (string.IsNullOrWhiteSpace(oldPassword))
            {
                ModelState.AddModelError("oldPassword", "Vui lòng nhập mật khẩu cũ");
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ModelState.AddModelError("newPassword", "Vui lòng nhập mật khẩu mới");
            }
            else if (newPassword.Length < 6)
            {
                ModelState.AddModelError("newPassword", "Mật khẩu mới phải có ít nhất 6 ký tự");
            }

            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError("confirmPassword", "Vui lòng xác nhận mật khẩu mới");
            }
            else if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Mật khẩu xác nhận không khớp");
            }

            // Check old password
            if (!string.IsNullOrWhiteSpace(oldPassword) && !PasswordHelper.VerifyPassword(oldPassword, user.PasswordHash ?? ""))
            {
                ModelState.AddModelError("oldPassword", "Mật khẩu cũ không đúng");
            }

            if (ModelState.IsValid)
            {
                user.PasswordHash = PasswordHelper.HashPassword(newPassword);
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            return View();
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}

