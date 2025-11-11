using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UsersController(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Users
        public async Task<IActionResult> Index(int? groupId = null)
        {
            var query = _context.Users.AsQueryable();

            // SuperAdmin có thể filter theo nhóm
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                if (groupId.HasValue)
                {
                    query = query.Where(u => u.GroupId == groupId.Value);
                }
                // Nếu không chọn nhóm thì hiển thị tất cả

                // Load groups cho dropdown
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                ViewBag.SelectedGroupId = groupId;
            }
            else
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    query = query.Where(u => u.GroupId == currentGroupId.Value);
                }
                else
                {
                    // Admin không có group, không thấy user nào
                    query = query.Where(u => false);
                }
            }

            return View(await query.OrderBy(u => u.Name).ToListAsync());
        }

        // GET: Users/Create
        public async Task<IActionResult> Create()
        {
            var isSuperAdmin = SessionHelper.IsSuperAdmin(HttpContext);
            var isAdmin = SessionHelper.IsAdmin(HttpContext);
            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.CanCreateAdmin = isAdmin || isSuperAdmin; // Admin và SuperAdmin đều có thể tạo Admin
            ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();

            // Load groups cho SuperAdmin hoặc Admin
            if (isSuperAdmin)
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }
            else if (isAdmin)
            {
                // Admin chỉ thấy nhóm của mình
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    ViewBag.Groups = await _context.Groups
                        .Where(g => g.Id == groupId.Value && g.IsActive)
                        .OrderBy(g => g.Name)
                        .ToListAsync();
                }
            }

            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,IsActive,Role,BankName,BankAccount,AccountHolderName,TelegramUserId,CassoWebhookSecret")] User user, string Username, string Password, int? GroupId)
        {
            var isSuperAdmin = SessionHelper.IsSuperAdmin(HttpContext);
            var isAdmin = SessionHelper.IsAdmin(HttpContext);
            var groupId = SessionHelper.GetGroupId(HttpContext);

            // Validate Username
            if (string.IsNullOrWhiteSpace(Username))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập là bắt buộc");
            }
            else
            {
                // Check if username already exists
                var usernameExists = await _context.Users.AnyAsync(u => u.Username == Username);
                if (usernameExists)
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng");
                }
                else
                {
                    user.Username = Username;
                }
            }

            // Validate Password
            if (string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError("Password", "Mật khẩu là bắt buộc");
            }
            else if (Password.Length < 6)
            {
                ModelState.AddModelError("Password", "Mật khẩu phải có ít nhất 6 ký tự");
            }

            // Admin và SuperAdmin đều có thể tạo Admin
            if (user.Role == "Admin" && !isAdmin && !isSuperAdmin)
            {
                ModelState.AddModelError("Role", "Chỉ Admin và SuperAdmin mới có thể tạo Admin");
            }

            // User thường không thể tạo Admin hoặc SuperAdmin
            if (!isAdmin && !isSuperAdmin)
            {
                if (user.Role != "User")
                {
                    user.Role = "User";
                }
            }

            if (ModelState.IsValid)
            {
                user.CreatedAt = DateTime.Now;

                // Set PasswordHash - password đã được validate ở trên, nên chắc chắn không rỗng và >= 6 ký tự
                // Đảm bảo password luôn được set khi tạo user mới
                if (string.IsNullOrWhiteSpace(Password))
                {
                    // Trường hợp này không nên xảy ra vì đã validate ở trên, nhưng để đảm bảo an toàn
                    ModelState.AddModelError("Password", "Mật khẩu là bắt buộc");
                    ViewBag.IsSuperAdmin = isSuperAdmin;
                    ViewBag.IsAdmin = isAdmin;
                    ViewBag.CanCreateAdmin = isAdmin || isSuperAdmin;
                    ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();
                    if (isSuperAdmin)
                    {
                        ViewBag.Groups = await _context.Groups
                            .Where(g => g.IsActive)
                            .OrderBy(g => g.Name)
                            .ToListAsync();
                    }
                    else if (isAdmin && groupId.HasValue)
                    {
                        ViewBag.Groups = await _context.Groups
                            .Where(g => g.Id == groupId.Value && g.IsActive)
                            .OrderBy(g => g.Name)
                            .ToListAsync();
                    }
                    return View(user);
                }

                // Hash password bằng BCrypt (an toàn hơn MD5)
                user.PasswordHash = PasswordHelper.HashPassword(Password);

                // Set GroupId
                if (isSuperAdmin)
                {
                    // SuperAdmin có thể quyết định user thuộc nhóm nào (hoặc không thuộc nhóm nào)
                    user.GroupId = GroupId;

                    // Nếu là Admin role thì phải có GroupId
                    if (user.Role == "Admin" && !GroupId.HasValue)
                    {
                        ModelState.AddModelError("GroupId", "Admin phải thuộc một nhóm");
                        ViewBag.IsSuperAdmin = isSuperAdmin;
                        ViewBag.IsAdmin = isAdmin;
                        ViewBag.CanCreateAdmin = isAdmin || isSuperAdmin;
                        ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();
                        ViewBag.Groups = await _context.Groups
                            .Where(g => g.IsActive)
                            .OrderBy(g => g.Name)
                            .ToListAsync();
                        return View(user);
                    }
                }
                else if (isAdmin)
                {
                    // Admin set group của mình (hoặc từ GroupId nếu có)
                    if (groupId.HasValue)
                    {
                        if (GroupId.HasValue && GroupId.Value == groupId.Value)
                        {
                            // Admin chỉ có thể set user vào nhóm của mình
                            user.GroupId = GroupId.Value;
                        }
                        else
                        {
                            user.GroupId = groupId.Value;
                        }
                    }

                    // Nếu Admin tạo user có role Admin, phải đảm bảo có GroupId
                    if (user.Role == "Admin" && !user.GroupId.HasValue)
                    {
                        ModelState.AddModelError("GroupId", "Admin phải thuộc một nhóm");
                        ViewBag.IsSuperAdmin = isSuperAdmin;
                        ViewBag.IsAdmin = isAdmin;
                        ViewBag.CanCreateAdmin = isAdmin || isSuperAdmin;
                        ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();
                        if (groupId.HasValue)
                        {
                            ViewBag.Groups = await _context.Groups
                                .Where(g => g.Id == groupId.Value && g.IsActive)
                                .OrderBy(g => g.Name)
                                .ToListAsync();
                        }
                        return View(user);
                    }
                }
                else
                {
                    // User thường không thể tạo user
                    TempData["ErrorMessage"] = "Bạn không có quyền tạo user.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Add(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm người dùng thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.CanCreateAdmin = isAdmin || isSuperAdmin;
            ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();
            if (isSuperAdmin)
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }
            else if (isAdmin && groupId.HasValue)
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.Id == groupId.Value && g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }
            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Check permission: SuperAdmin edit được tất cả, Admin chỉ edit users trong nhóm
            var isSuperAdmin = SessionHelper.IsSuperAdmin(HttpContext);
            var isAdmin = SessionHelper.IsAdmin(HttpContext);
            if (!isSuperAdmin)
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (!groupId.HasValue || user.GroupId != groupId.Value)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập user này.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.CanCreateAdmin = isAdmin || isSuperAdmin;
            ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();

            // Load groups cho SuperAdmin hoặc Admin
            if (isSuperAdmin)
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }
            else if (isAdmin)
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    ViewBag.Groups = await _context.Groups
                        .Where(g => g.Id == groupId.Value && g.IsActive)
                        .OrderBy(g => g.Name)
                        .ToListAsync();
                }
            }

            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IsActive,CreatedAt,BankName,BankAccount,AccountHolderName,TelegramUserId,CassoWebhookSecret")] User user, string? Username, string? NewPassword, int? GroupId, string? Role, IFormFile? avatarFile)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null)
            {
                return NotFound();
            }

            // Check permission
            var isSuperAdmin = SessionHelper.IsSuperAdmin(HttpContext);
            var isAdmin = SessionHelper.IsAdmin(HttpContext);
            if (!isSuperAdmin)
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (!groupId.HasValue || existingUser.GroupId != groupId.Value)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập user này.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Validate Role nếu Admin/SuperAdmin cập nhật
            if ((isAdmin || isSuperAdmin) && !string.IsNullOrWhiteSpace(Role))
            {
                // Chỉ SuperAdmin mới có thể set SuperAdmin
                if (Role == "SuperAdmin" && !isSuperAdmin)
                {
                    ModelState.AddModelError("Role", "Chỉ SuperAdmin mới có thể phân quyền SuperAdmin");
                    Role = existingUser.Role; // Giữ nguyên role cũ
                }

                // Admin chỉ có thể phân quyền user trong nhóm của mình thành Admin hoặc User
                if (isAdmin && !isSuperAdmin)
                {
                    if (Role == "SuperAdmin")
                    {
                        ModelState.AddModelError("Role", "Admin không thể phân quyền SuperAdmin");
                        Role = existingUser.Role; // Giữ nguyên role cũ
                    }
                    else if (Role == "Admin" && !existingUser.GroupId.HasValue)
                    {
                        ModelState.AddModelError("Role", "Admin phải thuộc một nhóm");
                        Role = existingUser.Role; // Giữ nguyên role cũ
                    }
                }

                // Chỉ cập nhật Role nếu hợp lệ
                if (ModelState.IsValid || !ModelState.ContainsKey("Role"))
                {
                    existingUser.Role = Role;
                }
            }

            // Validate username nếu Admin/SuperAdmin cập nhật
            if ((isAdmin || isSuperAdmin) && !string.IsNullOrWhiteSpace(Username))
            {
                // Check if username already exists (trừ user hiện tại)
                var usernameExists = await _context.Users.AnyAsync(u => u.Username == Username && u.Id != id);
                if (usernameExists)
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng");
                }
                else
                {
                    existingUser.Username = Username;
                }
            }

            // Validate password nếu có nhập
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                if (NewPassword.Length < 6)
                {
                    ModelState.AddModelError("NewPassword", "Mật khẩu phải có ít nhất 6 ký tự");
                }
            }

            // Validate avatar file nếu có upload
            if (avatarFile != null && avatarFile.Length > 0)
            {
                var (isValid, errorMessage) = AvatarHelper.ValidateAvatarFile(avatarFile, _configuration);
                if (!isValid)
                {
                    ModelState.AddModelError("AvatarFile", errorMessage ?? "File ảnh không hợp lệ");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingUser.Name = user.Name;
                    existingUser.IsActive = user.IsActive;
                    existingUser.BankName = user.BankName;
                    existingUser.BankAccount = user.BankAccount;
                    existingUser.AccountHolderName = user.AccountHolderName;
                    existingUser.TelegramUserId = user.TelegramUserId;
                    existingUser.CassoWebhookSecret = user.CassoWebhookSecret; // Có thể null nếu để trống

                    // Xử lý upload avatar
                    if (avatarFile != null && avatarFile.Length > 0)
                    {
                        // Xóa avatar cũ nếu có
                        AvatarHelper.DeleteAvatar(existingUser.AvatarPath, _webHostEnvironment);

                        // Lưu avatar mới
                        var newAvatarPath = await AvatarHelper.SaveAvatar(avatarFile, id, _webHostEnvironment, _configuration);
                        if (!string.IsNullOrEmpty(newAvatarPath))
                        {
                            existingUser.AvatarPath = newAvatarPath;
                        }
                    }

                    // Admin/SuperAdmin có thể cập nhật username (đã xử lý ở trên)
                    // Role đã được cập nhật ở trên (nếu hợp lệ)

                    // SuperAdmin có thể cập nhật GroupId cho bất kỳ user nào
                    // Admin chỉ có thể cập nhật GroupId trong nhóm của mình
                    if (isSuperAdmin)
                    {
                        existingUser.GroupId = GroupId;
                    }
                    else if (isAdmin)
                    {
                        // Admin chỉ có thể set GroupId trong nhóm của mình
                        var groupId = SessionHelper.GetGroupId(HttpContext);
                        if (groupId.HasValue)
                        {
                            if (GroupId.HasValue && GroupId.Value == groupId.Value)
                            {
                                existingUser.GroupId = GroupId.Value;
                            }
                            else
                            {
                                // Nếu không có GroupId mới, giữ nguyên hoặc set về nhóm của Admin
                                existingUser.GroupId = groupId.Value;
                            }

                            // Nếu set role Admin thì phải có GroupId
                            if (Role == "Admin" && !existingUser.GroupId.HasValue)
                            {
                                existingUser.GroupId = groupId.Value;
                            }
                        }
                    }

                    // Reset password nếu có nhập mật khẩu mới
                    if (!string.IsNullOrWhiteSpace(NewPassword))
                    {
                        existingUser.PasswordHash = PasswordHelper.HashPassword(NewPassword);
                    }

                    _context.Update(existingUser);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật người dùng thành công!" + (!string.IsNullOrWhiteSpace(NewPassword) ? " Mật khẩu đã được đặt lại." : "");
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
                return RedirectToAction(nameof(Index));
            }
            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.Banks = Helpers.QRCodeHelper.GetSupportedBanks().OrderBy(b => b.Name).ToList();
            if (isSuperAdmin)
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }
            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Check permission
                if (!SessionHelper.IsSuperAdmin(HttpContext))
                {
                    var groupId = SessionHelper.GetGroupId(HttpContext);
                    if (!groupId.HasValue || user.GroupId != groupId.Value)
                    {
                        return Forbid();
                    }
                }

                // Không cho xóa SuperAdmin hoặc Admin
                if (user.Role == "SuperAdmin" || user.Role == "Admin")
                {
                    TempData["ErrorMessage"] = "Không thể xóa SuperAdmin hoặc Admin. Vui lòng vô hiệu hóa thay vì xóa.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if user has expenses or payments
                var hasExpenses = await _context.Expenses.AnyAsync(e => e.PayerId == id);
                var hasParticipants = await _context.ExpenseParticipants.AnyAsync(ep => ep.UserId == id);
                var hasPayments = await _context.MonthlyPayments.AnyAsync(mp => mp.UserId == id);

                if (hasExpenses || hasParticipants || hasPayments)
                {
                    TempData["ErrorMessage"] = "Không thể xóa người dùng này vì đã có dữ liệu liên quan. Vui lòng vô hiệu hóa thay vì xóa.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa người dùng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Users/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Check permission
                if (!SessionHelper.IsSuperAdmin(HttpContext))
                {
                    var groupId = SessionHelper.GetGroupId(HttpContext);
                    if (!groupId.HasValue || user.GroupId != groupId.Value)
                    {
                        return Forbid();
                    }
                }

                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã {(user.IsActive ? "kích hoạt" : "vô hiệu hóa")} người dùng thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}

