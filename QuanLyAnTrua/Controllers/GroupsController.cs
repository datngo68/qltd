using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Groups
        public async Task<IActionResult> Index()
        {
            // Chỉ SuperAdmin mới xem được
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            var groups = await _context.Groups
                .Include(g => g.Creator)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            return View(groups);
        }

        // GET: Groups/Create
        public IActionResult Create()
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            return View();
        }

        // POST: Groups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] Group group)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            // Clear ModelState errors for CreatedBy and Creator since we set them manually
            ModelState.Remove("CreatedBy");
            ModelState.Remove("Creator");

            if (ModelState.IsValid)
            {
                var userId = SessionHelper.GetUserId(HttpContext);
                if (userId == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                group.CreatedBy = userId.Value;
                group.CreatedAt = DateTime.Now;
                group.IsActive = true;

                _context.Add(group);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo nhóm thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(group);
        }

        // GET: Groups/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Groups.FindAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            return View(group);
        }

        // POST: Groups/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,IsActive")] Group group)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            if (id != group.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingGroup = await _context.Groups.FindAsync(id);
                    if (existingGroup == null)
                    {
                        return NotFound();
                    }

                    existingGroup.Name = group.Name;
                    existingGroup.Description = group.Description;
                    existingGroup.IsActive = group.IsActive;

                    _context.Update(existingGroup);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cập nhật nhóm thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GroupExists(group.Id))
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

            return View(group);
        }

        // GET: Groups/Manage/5 - Tạo admin cho group
        public async Task<IActionResult> Manage(int? id)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Groups
                .Include(g => g.Users)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            ViewBag.GroupId = group.Id;
            ViewBag.GroupName = group.Name;
            ViewBag.ExistingAdmin = group.Users.FirstOrDefault(u => u.Role == "Admin");

            return View();
        }

        // POST: Groups/Manage/5 - Tạo admin cho group
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(int groupId, string username, string password, string name)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            var group = await _context.Groups.FindAsync(groupId);
            if (group == null)
            {
                return NotFound();
            }

            // Check if group already has an admin
            var existingAdmin = await _context.Users.FirstOrDefaultAsync(u => u.GroupId == groupId && u.Role == "Admin");
            if (existingAdmin != null)
            {
                ModelState.AddModelError("", "Nhóm này đã có admin. Vui lòng sử dụng chức năng sửa admin.");
                ViewBag.GroupId = groupId;
                ViewBag.GroupName = group.Name;
                ViewBag.ExistingAdmin = existingAdmin;
                return View();
            }

            // Validate
            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError("username", "Tên đăng nhập là bắt buộc");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("password", "Mật khẩu là bắt buộc");
            }
            else if (password.Length < 6)
            {
                ModelState.AddModelError("password", "Mật khẩu phải có ít nhất 6 ký tự");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("name", "Tên người dùng là bắt buộc");
            }

            // Check if username already exists
            var usernameExists = await _context.Users.AnyAsync(u => u.Username == username);
            if (usernameExists)
            {
                ModelState.AddModelError("username", "Tên đăng nhập đã được sử dụng");
            }

            if (ModelState.IsValid)
            {
                var adminUser = new User
                {
                    Name = name,
                    Username = username,
                    PasswordHash = PasswordHelper.HashPassword(password),
                    Role = "Admin",
                    GroupId = groupId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Add(adminUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo admin cho nhóm thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.GroupId = groupId;
            ViewBag.GroupName = group.Name;
            ViewBag.ExistingAdmin = existingAdmin;

            return View();
        }

        // POST: Groups/EditAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdmin(int groupId, int adminId, string username, string password, string name)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            var group = await _context.Groups.FindAsync(groupId);
            if (group == null)
            {
                return NotFound();
            }

            var admin = await _context.Users.FindAsync(adminId);
            if (admin == null || admin.GroupId != groupId || admin.Role != "Admin")
            {
                return NotFound();
            }

            // Validate
            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError("username", "Tên đăng nhập là bắt buộc");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("name", "Tên người dùng là bắt buộc");
            }

            // Check if username already exists (excluding current admin)
            var usernameExists = await _context.Users.AnyAsync(u => u.Username == username && u.Id != adminId);
            if (usernameExists)
            {
                ModelState.AddModelError("username", "Tên đăng nhập đã được sử dụng");
            }

            if (ModelState.IsValid)
            {
                admin.Name = name;
                admin.Username = username;

                // Only update password if provided
                if (!string.IsNullOrWhiteSpace(password))
                {
                    if (password.Length < 6)
                    {
                        ModelState.AddModelError("password", "Mật khẩu phải có ít nhất 6 ký tự");
                        ViewBag.GroupId = groupId;
                        ViewBag.GroupName = group.Name;
                        ViewBag.ExistingAdmin = admin;
                        return View("Manage");
                    }
                    admin.PasswordHash = PasswordHelper.HashPassword(password);
                }

                _context.Update(admin);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật admin thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.GroupId = groupId;
            ViewBag.GroupName = group.Name;
            ViewBag.ExistingAdmin = admin;

            return View("Manage");
        }

        // POST: Groups/DeleteAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin(int groupId, int adminId)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            var group = await _context.Groups.FindAsync(groupId);
            if (group == null)
            {
                return NotFound();
            }

            var admin = await _context.Users.FindAsync(adminId);
            if (admin == null || admin.GroupId != groupId || admin.Role != "Admin")
            {
                return NotFound();
            }

            _context.Users.Remove(admin);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa admin thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Groups/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Groups
                .Include(g => g.Creator)
                .Include(g => g.Users)
                .Include(g => g.Expenses)
                .Include(g => g.MonthlyPayments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            // Check if group has related data
            var hasUsers = group.Users.Any();
            var hasExpenses = group.Expenses.Any();
            var hasPayments = group.MonthlyPayments.Any();

            ViewBag.HasUsers = hasUsers;
            ViewBag.HasExpenses = hasExpenses;
            ViewBag.HasPayments = hasPayments;
            ViewBag.CanDelete = !hasUsers && !hasExpenses && !hasPayments;

            return View(group);
        }

        // POST: Groups/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Forbid();
            }

            var group = await _context.Groups
                .Include(g => g.Users)
                .Include(g => g.Expenses)
                .Include(g => g.MonthlyPayments)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            // Check if group has related data
            if (group.Users.Any() || group.Expenses.Any() || group.MonthlyPayments.Any())
            {
                TempData["ErrorMessage"] = "Không thể xóa nhóm này vì nhóm đang có dữ liệu liên quan (người dùng, chi phí, hoặc thanh toán).";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa nhóm thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool GroupExists(int id)
        {
            return _context.Groups.Any(e => e.Id == id);
        }
    }
}

