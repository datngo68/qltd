using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using QuanLyAnTrua.Models.ViewModels;
using Serilog;

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

            // Sử dụng helper để filter theo group
            query = QueryFilterHelper.FilterByGroup(query, HttpContext, groupId);

            // Load groups cho dropdown nếu cần
            var groups = await QueryFilterHelper.LoadGroupsForDropdownAsync(_context, HttpContext);
            if (groups != null)
            {
                ViewBag.Groups = groups;
                ViewBag.SelectedGroupId = groupId;
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
            // Sử dụng helper để filter users
            var query = QueryFilterHelper.FilterUsersByGroup(_context.Users, HttpContext, groupId, activeOnly: true);

            // Load groups cho dropdown nếu cần
            var groups = await QueryFilterHelper.LoadGroupsForDropdownAsync(_context, HttpContext);
            if (groups != null)
            {
                ViewBag.Groups = groups;
                ViewBag.SelectedGroupId = groupId;
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
            // Parse ParticipantAmounts từ Request.Form nếu SplitType = Custom
            if (viewModel.SplitType == SplitType.Custom)
            {
                viewModel.ParticipantAmounts = ExpenseFormHelper.ParseParticipantAmounts(Request.Form);
            }

            if (viewModel.ParticipantIds == null || !viewModel.ParticipantIds.Any())
            {
                ModelState.AddModelError("ParticipantIds", "Vui lòng chọn ít nhất một người sử dụng");
            }

            // Validate SplitType = Custom sử dụng helper
            ExpenseFormHelper.ValidateCustomSplitAmounts(viewModel, ModelState);

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
                            UserId = participantId,
                            Amount = ExpenseFormHelper.CalculateParticipantAmount(
                                viewModel.SplitType, 
                                participantId, 
                                viewModel.ParticipantAmounts)
                        };
                        _context.Add(participant);
                    }
                }

                await _context.SaveChangesAsync();

                // Gửi nhắc Telegram nếu được yêu cầu
                var sendTelegram = Request.Form["SendTelegram"].ToString() == "true";
                if (sendTelegram)
                {
                    await SendTelegramNotificationsAsync(expense, viewModel.ParticipantIds ?? new List<int>());
                }

                TempData["SuccessMessage"] = "Thêm chi phí thành công!";
                return RedirectToAction(nameof(Index));
            }

            // Reload users for dropdown
            var userQueryReload = QueryFilterHelper.FilterUsersByGroup(_context.Users, HttpContext, groupId, activeOnly: true);

            // Load groups cho dropdown nếu cần
            var groupsReload = await QueryFilterHelper.LoadGroupsForDropdownAsync(_context, HttpContext);
            if (groupsReload != null)
            {
                ViewBag.Groups = groupsReload;
                ViewBag.SelectedGroupId = null; // Reset khi validation error
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
            if (!QueryFilterHelper.CanAccessGroup(HttpContext, expense.GroupId))
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

            // Nếu SuperAdmin và không có groupId, mặc định filter theo group của expense
            var effectiveGroupId = groupId ?? expense.GroupId;
            var userQuery = QueryFilterHelper.FilterUsersByGroup(_context.Users, HttpContext, effectiveGroupId, activeOnly: true);

            // Load groups cho dropdown nếu cần
            var groups = await QueryFilterHelper.LoadGroupsForDropdownAsync(_context, HttpContext);
            if (groups != null)
            {
                ViewBag.Groups = groups;
                ViewBag.SelectedGroupId = effectiveGroupId;
            }

            var activeUsers = await userQuery.OrderBy(u => u.Name).ToListAsync();

            // Xác định SplitType: nếu tất cả participants đều có Amount = null thì là Equal, ngược lại là Custom
            var hasCustomAmounts = expense.Participants.Any(p => p.Amount.HasValue);
            var participantAmounts = new Dictionary<int, decimal>();

            if (hasCustomAmounts)
            {
                foreach (var participant in expense.Participants)
                {
                    if (participant.Amount.HasValue)
                    {
                        participantAmounts[participant.UserId] = participant.Amount.Value;
                    }
                }
            }

            var viewModel = new ExpenseViewModel
            {
                Id = expense.Id,
                Amount = expense.Amount,
                PayerId = expense.PayerId,
                ExpenseDate = expense.ExpenseDate,
                Description = expense.Description,
                ParticipantIds = expense.Participants.Select(p => p.UserId).ToList(),
                SplitType = hasCustomAmounts ? SplitType.Custom : SplitType.Equal,
                ParticipantAmounts = participantAmounts,
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

            // Parse ParticipantAmounts từ Request.Form nếu SplitType = Custom
            if (viewModel.SplitType == SplitType.Custom)
            {
                viewModel.ParticipantAmounts = ExpenseFormHelper.ParseParticipantAmounts(Request.Form);
            }

            if (viewModel.ParticipantIds == null || !viewModel.ParticipantIds.Any())
            {
                ModelState.AddModelError("ParticipantIds", "Vui lòng chọn ít nhất một người sử dụng");
            }

            // Validate SplitType = Custom sử dụng helper
            ExpenseFormHelper.ValidateCustomSplitAmounts(viewModel, ModelState);

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

                    // Update participants và add new ones
                    foreach (var participantId in newParticipantIds)
                    {
                        var existingParticipant = expense.Participants.FirstOrDefault(p => p.UserId == participantId);

                        if (existingParticipant != null)
                        {
                            // Update Amount cho participant đã tồn tại
                            existingParticipant.Amount = ExpenseFormHelper.CalculateParticipantAmount(
                                viewModel.SplitType, 
                                participantId, 
                                viewModel.ParticipantAmounts);
                        }
                        else
                        {
                            // Add new participant
                            var participant = new ExpenseParticipant
                            {
                                ExpenseId = expense.Id,
                                UserId = participantId,
                                Amount = ExpenseFormHelper.CalculateParticipantAmount(
                                    viewModel.SplitType, 
                                    participantId, 
                                    viewModel.ParticipantAmounts)
                            };
                            _context.Add(participant);
                        }
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
            // Nếu SuperAdmin và không có groupId, lấy expense để lấy groupId mặc định
            if (SessionHelper.IsSuperAdmin(HttpContext) && !groupId.HasValue)
            {
                var expenseForGroup = await _context.Expenses.FindAsync(id);
                if (expenseForGroup != null && expenseForGroup.GroupId.HasValue)
                {
                    groupId = expenseForGroup.GroupId;
                }
            }

            var userQuery = QueryFilterHelper.FilterUsersByGroup(_context.Users, HttpContext, groupId, activeOnly: true);

            // Load groups cho dropdown nếu cần
            var groupsReload = await QueryFilterHelper.LoadGroupsForDropdownAsync(_context, HttpContext);
            if (groupsReload != null)
            {
                ViewBag.Groups = groupsReload;
                ViewBag.SelectedGroupId = groupId; // Giữ lại groupId khi validation error
            }

            viewModel.AllUsers = await userQuery.OrderBy(u => u.Name).ToListAsync();

            return View(viewModel);
        }

        private bool ExpenseExists(int id)
        {
            return _context.Expenses.Any(e => e.Id == id);
        }

        /// <summary>
        /// Escape các ký tự đặc biệt trong Markdown để tránh lỗi parsing
        /// Chỉ escape trong text content, không escape trong format tags
        /// </summary>
        private string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Escape các ký tự đặc biệt trong Markdown (Markdown cũ)
            // Lưu ý: Không escape * và _ nếu chúng được dùng cho bold/italic
            // Chỉ escape các ký tự có thể gây conflict với link format [text](url)
            return text
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("~", "\\~")
                .Replace("`", "\\`")
                .Replace(">", "\\>")
                .Replace("#", "\\#")
                .Replace("+", "\\+")
                .Replace("-", "\\-")
                .Replace("=", "\\=")
                .Replace("|", "\\|")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace(".", "\\.")
                .Replace("!", "\\!");
        }

        /// <summary>
        /// Gửi thông báo Telegram cho các participants khi có expense mới
        /// </summary>
        private async Task SendTelegramNotificationsAsync(Expense expense, List<int> participantIds)
        {
            try
            {
                var payer = await _context.Users.FindAsync(expense.PayerId);
                if (payer == null) return;

                // Lấy GroupId từ expense
                if (!expense.GroupId.HasValue)
                {
                    Log.Warning("Expense {ExpenseId} không có GroupId, không thể tạo link Group", expense.Id);
                    return;
                }

                var participants = await _context.Users
                    .Where(u => participantIds.Contains(u.Id) && !string.IsNullOrEmpty(u.TelegramUserId))
                    .ToListAsync();

                if (!participants.Any())
                {
                    Log.Information("Không có participant nào có TelegramUserId cho expense {ExpenseId}", expense.Id);
                    return;
                }

                var expenseDate = expense.ExpenseDate.ToString("dd/MM/yyyy");
                var description = string.IsNullOrEmpty(expense.Description) ? "Không có mô tả" : expense.Description;

                // Tạo hoặc lấy SharedReport cho Group theo tháng/năm của chi phí
                // Tìm SharedReport được tạo trong cùng tháng/năm với expense
                var expenseYear = expense.ExpenseDate.Year;
                var expenseMonth = expense.ExpenseDate.Month;

                var sharedReport = await _context.SharedReports
                    .Where(sr => sr.ReportType == "Group"
                        && sr.GroupId == expense.GroupId.Value
                        && sr.IsActive
                        && sr.CreatedAt.Year == expenseYear
                        && sr.CreatedAt.Month == expenseMonth)
                    .OrderByDescending(sr => sr.CreatedAt)
                    .FirstOrDefaultAsync();

                string publicViewUrl;
                if (sharedReport != null && (!sharedReport.ExpiresAt.HasValue || sharedReport.ExpiresAt.Value > DateTime.Now))
                {
                    // Sử dụng link hiện có
                    publicViewUrl = Url.Action("PublicView", "Reports", new { token = sharedReport.Token }, Request.Scheme)!;
                }
                else
                {
                    // Tạo link mới cho Group
                    string token;
                    do
                    {
                        token = TokenHelper.GenerateSecureToken(32);
                    } while (await _context.SharedReports.AnyAsync(sr => sr.Token == token));

                    var newSharedReport = new SharedReport
                    {
                        Token = token,
                        ReportType = "Group",
                        GroupId = expense.GroupId.Value,
                        CreatedBy = expense.PayerId,
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddMonths(3), // Hết hạn sau 3 tháng
                        IsActive = true
                    };

                    _context.Add(newSharedReport);
                    await _context.SaveChangesAsync();

                    publicViewUrl = Url.Action("PublicView", "Reports", new { token = token }, Request.Scheme)!;
                }

                // Load expense với participants để lấy Amount
                var expenseWithParticipants = await _context.Expenses
                    .Include(e => e.Participants)
                    .FirstOrDefaultAsync(e => e.Id == expense.Id);

                // Gửi message cho từng participant song song để tránh chờ tuần tự
                var notificationTasks = participants
                    .Where(participant => participant.Id != expense.PayerId)
                    .Select(async participant =>
                    {
                        try
                        {
                            // Tính số tiền participant phải trả
                            decimal participantAmount = 0;
                            var expenseParticipant = expenseWithParticipants?.Participants.FirstOrDefault(p => p.UserId == participant.Id);
                            if (expenseParticipant != null)
                            {
                                if (expenseParticipant.Amount.HasValue)
                                {
                                    // Dùng số tiền cụ thể
                                    participantAmount = expenseParticipant.Amount.Value;
                                }
                                else
                                {
                                    // Chia đều: tính số tiền còn lại sau khi trừ các custom amounts
                                    var participantsWithoutAmount = expenseWithParticipants!.Participants.Where(p => !p.Amount.HasValue).ToList();
                                    var totalCustomAmount = expenseWithParticipants.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount.Value);
                                    var remainingAmount = expense.Amount - totalCustomAmount;
                                    participantAmount = participantsWithoutAmount.Count > 0
                                        ? Math.Round(remainingAmount / participantsWithoutAmount.Count, 2)
                                        : 0;
                                }
                            }

                            // Tạo message với URL trực tiếp (không dùng parse mode)
                            // Telegram sẽ tự động detect URL và làm cho nó clickable
                            var message = $"💰 Thông báo chi phí mới\n\n" +
                                         $"📅 Ngày: {expenseDate}\n" +
                                         $"💵 Tổng chi phí: {expense.Amount:N0} đ\n" +
                                         $"👤 Người chi: {payer.Name}\n" +
                                         $"📝 Mô tả: {description}\n\n" +
                                         $"Bạn cần thanh toán: {participantAmount:N0} đ\n\n" +
                                         $"🔗 Xem chi tiết và thanh toán:\n{publicViewUrl}";

                            // Log URL để debug
                            Log.Information("Gửi Telegram message với URL: {Url} cho user {UserId}", publicViewUrl, participant.Id);

                            // Gửi message không dùng parse mode để Telegram tự động detect URL
                            // Hoặc có thể dùng Markdown nếu muốn giữ format bold
                            var sent = await TelegramHelper.SendMessageAsync(participant.TelegramUserId!, message, null);
                            if (sent)
                            {
                                Log.Information("Đã gửi Telegram notification cho user {UserId} ({UserName}) về expense {ExpenseId}",
                                    participant.Id, participant.Name, expense.Id);
                            }
                            else
                            {
                                Log.Warning("Không thể gửi Telegram notification cho user {UserId} về expense {ExpenseId}",
                                    participant.Id, expense.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Lỗi khi gửi Telegram notification cho user {UserId} về expense {ExpenseId}",
                                participant.Id, expense.Id);
                        }
                    })
                    .ToList();

                await Task.WhenAll(notificationTasks);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi khi gửi Telegram notifications cho expense {ExpenseId}", expense.Id);
            }
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

