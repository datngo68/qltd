using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using QuanLyAnTrua.Models.ViewModels;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Expenses
        public async Task<IActionResult> Index(int? groupId = null, int? month = null, int? year = null)
        {
            var query = _context.Expenses
                .Include(e => e.Payer)
                .Include(e => e.Participants)
                    .ThenInclude(ep => ep.User)
                .AsQueryable();

            // SuperAdmin có thể filter theo nhóm
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                if (groupId.HasValue)
                {
                    query = query.Where(e => e.GroupId == groupId.Value);
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
                    query = query.Where(e => e.GroupId == currentGroupId.Value);
                }
                else
                {
                    query = query.Where(e => false); // Admin không có group, không thấy expense nào
                }
            }

            // Lọc theo tháng/năm (áp dụng cho tất cả người dùng)
            if (year.HasValue)
            {
                if (month.HasValue)
                {
                    // Lọc theo tháng và năm cụ thể
                    query = query.Where(e => e.ExpenseDate.Year == year.Value && e.ExpenseDate.Month == month.Value);
                }
                else
                {
                    // Chỉ lọc theo năm
                    query = query.Where(e => e.ExpenseDate.Year == year.Value);
                }
            }
            else if (month.HasValue)
            {
                // Nếu chỉ có tháng mà không có năm, lọc theo tháng của năm hiện tại
                var currentYear = DateTime.Now.Year;
                query = query.Where(e => e.ExpenseDate.Year == currentYear && e.ExpenseDate.Month == month.Value);
            }

            // Lưu giá trị filter vào ViewBag để hiển thị trong dropdown
            ViewBag.SelectedMonth = month;
            ViewBag.SelectedYear = year;

            var expenses = await query
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();

            return View(expenses);
        }

        // GET: Expenses/Create
        public async Task<IActionResult> Create(int? groupId = null)
        {
            var query = _context.Users.Where(u => u.IsActive).AsQueryable();

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
                // Filter users by GroupId
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    query = query.Where(u => u.GroupId == currentGroupId.Value);
                }
                else
                {
                    // User thường có thể không có GroupId, cho phép chọn bản thân
                    if (SessionHelper.IsUser(HttpContext))
                    {
                        var currentUserId = SessionHelper.GetUserId(HttpContext);
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

            var activeUsers = await query.OrderBy(u => u.Name).ToListAsync();

            // Get logged in user
            var loggedInUserId = HttpContext.Session.GetInt32("UserId");
            var defaultPayerId = loggedInUserId ?? activeUsers.FirstOrDefault()?.Id ?? 0;

            // User thường chỉ có thể chọn bản thân làm payer
            if (SessionHelper.IsUser(HttpContext) && loggedInUserId.HasValue)
            {
                defaultPayerId = loggedInUserId.Value;
            }

            // Default check all active users
            var defaultParticipantIds = activeUsers.Select(u => u.Id).ToList();

            var viewModel = new ExpenseViewModel
            {
                AllUsers = activeUsers,
                ExpenseDate = DateTime.Today,
                PayerId = defaultPayerId,
                ParticipantIds = defaultParticipantIds
            };

            return View(viewModel);
        }

        // POST: Expenses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExpenseViewModel viewModel, int? groupId = null)
        {
            if (viewModel.ParticipantIds == null || !viewModel.ParticipantIds.Any())
            {
                ModelState.AddModelError("ParticipantIds", "Vui lòng chọn ít nhất một người sử dụng");
            }

            // User thường chỉ có thể chọn bản thân làm payer
            var currentUserId = SessionHelper.GetUserId(HttpContext);
            if (SessionHelper.IsUser(HttpContext) && currentUserId.HasValue)
            {
                if (viewModel.PayerId != currentUserId.Value)
                {
                    ModelState.AddModelError("PayerId", "Bạn chỉ có thể chọn bản thân làm người chi tiền");
                }
            }

            if (ModelState.IsValid)
            {
                // Set GroupId
                int? expenseGroupId = null;
                if (!SessionHelper.IsSuperAdmin(HttpContext))
                {
                    expenseGroupId = SessionHelper.GetGroupId(HttpContext);
                }
                else
                {
                    // SuperAdmin có thể set GroupId từ parameter hoặc lấy từ payer
                    if (groupId.HasValue)
                    {
                        expenseGroupId = groupId;
                    }
                    else
                    {
                        var payer = await _context.Users.FindAsync(viewModel.PayerId);
                        if (payer != null && payer.GroupId.HasValue)
                        {
                            expenseGroupId = payer.GroupId;
                        }
                    }
                }

                var expense = new Expense
                {
                    Amount = viewModel.Amount,
                    PayerId = viewModel.PayerId,
                    ExpenseDate = viewModel.ExpenseDate,
                    Description = viewModel.Description,
                    GroupId = expenseGroupId,
                    CreatedAt = DateTime.Now
                };

                _context.Add(expense);
                await _context.SaveChangesAsync();

                // Add participants
                if (viewModel.ParticipantIds != null)
                {
                    foreach (var participantId in viewModel.ParticipantIds)
                    {
                        var participant = new ExpenseParticipant
                        {
                            ExpenseId = expense.Id,
                            UserId = participantId
                        };
                        _context.Add(participant);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm chi phí thành công!";
                return RedirectToAction(nameof(Index));
            }

            // Reload users for dropdown
            var userQueryReload = _context.Users.Where(u => u.IsActive).AsQueryable();

            // SuperAdmin có thể filter theo nhóm
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                // Load groups cho dropdown
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                ViewBag.SelectedGroupId = null; // Reset khi validation error

                // Nếu có groupId trong form, filter theo groupId
                // Note: Cần thêm groupId vào form nếu muốn giữ lại khi validation error
            }
            else
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    userQueryReload = userQueryReload.Where(u => u.GroupId == currentGroupId.Value);
                }
                else
                {
                    // User thường có thể không có GroupId, cho phép chọn bản thân
                    if (SessionHelper.IsUser(HttpContext))
                    {
                        var currentUserIdReload = SessionHelper.GetUserId(HttpContext);
                        if (currentUserIdReload.HasValue)
                        {
                            userQueryReload = userQueryReload.Where(u => u.Id == currentUserIdReload.Value);
                        }
                        else
                        {
                            userQueryReload = userQueryReload.Where(u => false);
                        }
                    }
                    else
                    {
                        userQueryReload = userQueryReload.Where(u => false);
                    }
                }
            }

            viewModel.AllUsers = await userQueryReload.OrderBy(u => u.Name).ToListAsync();

            return View(viewModel);
        }

        // GET: Expenses/Edit/5
        public async Task<IActionResult> Edit(int? id, int? groupId = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .Include(e => e.Participants)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
            {
                return NotFound();
            }

            // Check permission
            var currentUserId = SessionHelper.GetUserId(HttpContext);
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (!currentGroupId.HasValue || expense.GroupId != currentGroupId.Value)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập chi phí này.";
                    return RedirectToAction(nameof(Index));
                }

                // User thường chỉ có thể sửa expense của chính mình (expense mà mình là payer)
                if (SessionHelper.IsUser(HttpContext) && currentUserId.HasValue)
                {
                    if (expense.PayerId != currentUserId.Value)
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ có thể sửa chi phí của chính mình.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();

            // SuperAdmin có thể filter theo nhóm
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                if (groupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                }
                else if (expense.GroupId.HasValue)
                {
                    // Nếu không chọn groupId, mặc định filter theo group của expense
                    userQuery = userQuery.Where(u => u.GroupId == expense.GroupId.Value);
                }
                // Nếu không có groupId thì hiển thị tất cả

                // Load groups cho dropdown
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                ViewBag.SelectedGroupId = groupId ?? expense.GroupId;
            }
            else
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == currentGroupId.Value);
                }
                else
                {
                    userQuery = userQuery.Where(u => false);
                }
            }

            var activeUsers = await userQuery.OrderBy(u => u.Name).ToListAsync();

            var viewModel = new ExpenseViewModel
            {
                Id = expense.Id,
                Amount = expense.Amount,
                PayerId = expense.PayerId,
                ExpenseDate = expense.ExpenseDate,
                Description = expense.Description,
                ParticipantIds = expense.Participants.Select(p => p.UserId).ToList(),
                AllUsers = activeUsers
            };

            return View(viewModel);
        }

        // POST: Expenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExpenseViewModel viewModel, int? groupId = null)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (viewModel.ParticipantIds == null || !viewModel.ParticipantIds.Any())
            {
                ModelState.AddModelError("ParticipantIds", "Vui lòng chọn ít nhất một người sử dụng");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var expense = await _context.Expenses
                        .Include(e => e.Participants)
                        .FirstOrDefaultAsync(e => e.Id == id);

                    if (expense == null)
                    {
                        return NotFound();
                    }

                    // Check permission
                    var currentUserId = SessionHelper.GetUserId(HttpContext);
                    if (!SessionHelper.IsSuperAdmin(HttpContext))
                    {
                        var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                        if (!currentGroupId.HasValue || expense.GroupId != currentGroupId.Value)
                        {
                            TempData["ErrorMessage"] = "Bạn không có quyền truy cập chi phí này.";
                            return RedirectToAction(nameof(Index));
                        }

                        // User thường chỉ có thể sửa expense của chính mình (expense mà mình là payer)
                        if (SessionHelper.IsUser(HttpContext) && currentUserId.HasValue)
                        {
                            if (expense.PayerId != currentUserId.Value)
                            {
                                TempData["ErrorMessage"] = "Bạn chỉ có thể sửa chi phí của chính mình.";
                                return RedirectToAction(nameof(Index));
                            }

                            // User thường chỉ có thể chọn bản thân làm payer
                            if (viewModel.PayerId != currentUserId.Value)
                            {
                                ModelState.AddModelError("PayerId", "Bạn chỉ có thể chọn bản thân làm người chi tiền");
                                // Reload users for dropdown
                                var userQueryReload = _context.Users.Where(u => u.IsActive).AsQueryable();
                                var currentGroupId2 = SessionHelper.GetGroupId(HttpContext);
                                if (currentGroupId2.HasValue)
                                {
                                    userQueryReload = userQueryReload.Where(u => u.GroupId == currentGroupId2.Value);
                                }
                                viewModel.AllUsers = await userQueryReload.OrderBy(u => u.Name).ToListAsync();
                                return View(viewModel);
                            }
                        }
                    }

                    // Update expense properties
                    expense.Amount = viewModel.Amount;
                    expense.PayerId = viewModel.PayerId;
                    expense.ExpenseDate = viewModel.ExpenseDate;
                    expense.Description = viewModel.Description;

                    // Update participants - remove old ones
                    var existingParticipantIds = expense.Participants.Select(p => p.UserId).ToList();
                    var newParticipantIds = viewModel.ParticipantIds ?? new List<int>();

                    // Remove participants that are no longer selected
                    var participantsToRemove = expense.Participants
                        .Where(p => !newParticipantIds.Contains(p.UserId))
                        .ToList();
                    foreach (var participant in participantsToRemove)
                    {
                        _context.Remove(participant);
                    }

                    // Add new participants
                    var participantsToAdd = newParticipantIds
                        .Where(pid => !existingParticipantIds.Contains(pid))
                        .ToList();
                    foreach (var participantId in participantsToAdd)
                    {
                        var participant = new ExpenseParticipant
                        {
                            ExpenseId = expense.Id,
                            UserId = participantId
                        };
                        _context.Add(participant);
                    }

                    _context.Update(expense);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật chi phí thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExpenseExists(viewModel.Id))
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

            // Reload users for dropdown
            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();

            // SuperAdmin có thể filter theo nhóm
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                if (groupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                }
                else
                {
                    // Lấy expense để lấy groupId mặc định
                    var expense = await _context.Expenses.FindAsync(id);
                    if (expense != null && expense.GroupId.HasValue)
                    {
                        userQuery = userQuery.Where(u => u.GroupId == expense.GroupId.Value);
                        groupId = expense.GroupId;
                    }
                }

                // Load groups cho dropdown
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                ViewBag.SelectedGroupId = groupId; // Giữ lại groupId khi validation error
            }
            else
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == currentGroupId.Value);
                }
                else
                {
                    userQuery = userQuery.Where(u => false);
                }
            }

            viewModel.AllUsers = await userQuery.OrderBy(u => u.Name).ToListAsync();

            return View(viewModel);
        }

        private bool ExpenseExists(int id)
        {
            return _context.Expenses.Any(e => e.Id == id);
        }

        // GET: Expenses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .Include(e => e.Payer)
                .Include(e => e.Participants)
                    .ThenInclude(ep => ep.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (expense == null)
            {
                return NotFound();
            }

            // Check permission
            var currentUserIdDelete = SessionHelper.GetUserId(HttpContext);
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (!groupId.HasValue || expense.GroupId != groupId.Value)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập chi phí này.";
                    return RedirectToAction(nameof(Index));
                }

                // User thường chỉ có thể xóa expense của chính mình (expense mà mình là payer)
                if (SessionHelper.IsUser(HttpContext) && currentUserIdDelete.HasValue)
                {
                    if (expense.PayerId != currentUserIdDelete.Value)
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ có thể xóa chi phí của chính mình.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            return View(expense);
        }

        // POST: Expenses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                // Check permission
                var currentUserIdDelete = SessionHelper.GetUserId(HttpContext);
                if (!SessionHelper.IsSuperAdmin(HttpContext))
                {
                    var groupId = SessionHelper.GetGroupId(HttpContext);
                    if (!groupId.HasValue || expense.GroupId != groupId.Value)
                    {
                        TempData["ErrorMessage"] = "Bạn không có quyền truy cập chi phí này.";
                        return RedirectToAction(nameof(Index));
                    }

                    // User thường chỉ có thể xóa expense của chính mình (expense mà mình là payer)
                    if (SessionHelper.IsUser(HttpContext) && currentUserIdDelete.HasValue)
                    {
                        if (expense.PayerId != currentUserIdDelete.Value)
                        {
                            TempData["ErrorMessage"] = "Bạn chỉ có thể xóa chi phí của chính mình.";
                            return RedirectToAction(nameof(Index));
                        }
                    }
                }

                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa chi phí thành công!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

