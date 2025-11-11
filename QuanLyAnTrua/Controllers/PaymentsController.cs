using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using QuanLyAnTrua.Models.ViewModels;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Payments/Index?year=2025&month=11&groupId=1
        public async Task<IActionResult> Index(int? year, int? month, int? groupId = null)
        {
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            // Load groups cho dropdown (chỉ SuperAdmin)
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                ViewBag.SelectedGroupId = groupId;
            }

            var report = await GetMonthlyReportAsync(currentYear, currentMonth, groupId);
            return View(report);
        }

        // GET: Payments/Create
        public async Task<IActionResult> Create(int? year, int? month, int? userId)
        {
            // User thường không thể tạo payment, chỉ theo dõi
            if (SessionHelper.IsUser(HttpContext))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thêm thanh toán. Chỉ có thể theo dõi.";
                return RedirectToAction(nameof(Index), new { year = year ?? DateTime.Now.Year, month = month ?? DateTime.Now.Month });
            }

            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            var viewModel = new MonthlyPayment
            {
                Year = currentYear,
                Month = currentMonth,
                PaidDate = DateTime.Today,
                UserId = userId ?? 0
            };

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                }
                else
                {
                    // User thường có thể không có GroupId, cho phép chọn bản thân
                    if (SessionHelper.IsUser(HttpContext))
                    {
                        var currentUserId = SessionHelper.GetUserId(HttpContext);
                        if (currentUserId.HasValue)
                        {
                            userQuery = userQuery.Where(u => u.Id == currentUserId.Value);
                            // Set default UserId cho user thường
                            viewModel.UserId = currentUserId.Value;
                        }
                        else
                        {
                            userQuery = userQuery.Where(u => false);
                        }
                    }
                    else
                    {
                        userQuery = userQuery.Where(u => false);
                    }
                }
            }

            ViewBag.Users = await userQuery.OrderBy(u => u.Name).ToListAsync();

            // Load danh sách creditors (có thể là người được thanh toán)
            // Creditors có thể là bất kỳ user nào trong nhóm (hoặc tất cả nếu SuperAdmin)
            ViewBag.Creditors = await userQuery.OrderBy(u => u.Name).ToListAsync();

            return View(viewModel);
        }

        // POST: Payments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MonthlyPayment monthlyPayment)
        {
            // User thường không thể tạo payment, chỉ theo dõi
            if (SessionHelper.IsUser(HttpContext))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thêm thanh toán. Chỉ có thể theo dõi.";
                return RedirectToAction(nameof(Index), new { year = monthlyPayment.Year, month = monthlyPayment.Month });
            }

            // Clear all validation errors first
            ModelState.Clear();

            // Validate required fields manually
            bool isValid = true;

            if (monthlyPayment.UserId <= 0)
            {
                ModelState.AddModelError("UserId", "Vui lòng chọn người dùng");
                isValid = false;
            }

            if (monthlyPayment.Year < 2000 || monthlyPayment.Year > 2100)
            {
                ModelState.AddModelError("Year", "Năm không hợp lệ");
                isValid = false;
            }

            if (monthlyPayment.Month < 1 || monthlyPayment.Month > 12)
            {
                ModelState.AddModelError("Month", "Tháng phải từ 1 đến 12");
                isValid = false;
            }

            if (monthlyPayment.PaidAmount <= 0)
            {
                ModelState.AddModelError("PaidAmount", "Số tiền phải lớn hơn 0");
                isValid = false;
            }

            if (isValid)
            {
                try
                {
                    // Set GroupId
                    int? groupId = null;
                    if (!SessionHelper.IsSuperAdmin(HttpContext))
                    {
                        groupId = SessionHelper.GetGroupId(HttpContext);
                    }
                    else
                    {
                        var user = await _context.Users.FindAsync(monthlyPayment.UserId);
                        if (user != null && user.GroupId.HasValue)
                        {
                            groupId = user.GroupId;
                        }
                    }
                    // Nếu không có CreditorId, để null (sẽ áp dụng logic FIFO)
                    // Nếu có CreditorId, validate creditor tồn tại
                    if (monthlyPayment.CreditorId.HasValue)
                    {
                        var creditor = await _context.Users.FindAsync(monthlyPayment.CreditorId.Value);
                        if (creditor == null || !creditor.IsActive)
                        {
                            ModelState.AddModelError("CreditorId", "Người được thanh toán không hợp lệ");
                            isValid = false;
                        }
                    }

                    if (isValid)
                    {
                        monthlyPayment.GroupId = groupId;
                        monthlyPayment.Status = "Confirmed"; // Admin tạo trực tiếp thì tự động xác nhận

                        _context.Add(monthlyPayment);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Thêm thanh toán thành công!";
                        return RedirectToAction(nameof(Index), new { year = monthlyPayment.Year, month = monthlyPayment.Month });
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu: " + ex.Message);
                }
            }

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                }
                else
                {
                    userQuery = userQuery.Where(u => false);
                }
            }

            ViewBag.Users = await userQuery.OrderBy(u => u.Name).ToListAsync();
            ViewBag.Creditors = await userQuery.OrderBy(u => u.Name).ToListAsync();

            return View(monthlyPayment);
        }

        // GET: Payments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var monthlyPayment = await _context.MonthlyPayments.FindAsync(id);
            if (monthlyPayment == null)
            {
                return NotFound();
            }

            // Check permission
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (!groupId.HasValue || monthlyPayment.GroupId != groupId.Value)
                {
                    return Forbid();
                }
            }

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                }
                else
                {
                    userQuery = userQuery.Where(u => false);
                }
            }

            ViewBag.Users = await userQuery.OrderBy(u => u.Name).ToListAsync();
            ViewBag.Creditors = await userQuery.OrderBy(u => u.Name).ToListAsync();

            return View(monthlyPayment);
        }

        // POST: Payments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,CreditorId,Year,Month,PaidAmount,PaidDate,Notes")] MonthlyPayment monthlyPayment)
        {
            if (id != monthlyPayment.Id)
            {
                return NotFound();
            }

            var existingPayment = await _context.MonthlyPayments.FindAsync(id);
            if (existingPayment == null)
            {
                return NotFound();
            }

            // Check permission
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (!groupId.HasValue || existingPayment.GroupId != groupId.Value)
                {
                    return Forbid();
                }
            }

            // Validate CreditorId nếu có
            if (monthlyPayment.CreditorId.HasValue)
            {
                var creditor = await _context.Users.FindAsync(monthlyPayment.CreditorId.Value);
                if (creditor == null || !creditor.IsActive)
                {
                    ModelState.AddModelError("CreditorId", "Người được thanh toán không hợp lệ");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Giữ nguyên GroupId và Status từ payment cũ
                    monthlyPayment.GroupId = existingPayment.GroupId;
                    monthlyPayment.Status = existingPayment.Status;

                    _context.Update(monthlyPayment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật thanh toán thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MonthlyPaymentExists(monthlyPayment.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index), new { year = monthlyPayment.Year, month = monthlyPayment.Month });
            }

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                }
                else
                {
                    userQuery = userQuery.Where(u => false);
                }
            }

            ViewBag.Users = await userQuery.OrderBy(u => u.Name).ToListAsync();
            ViewBag.Creditors = await userQuery.OrderBy(u => u.Name).ToListAsync();

            return View(monthlyPayment);
        }

        // GET: Payments/GenerateQRCode
        [AllowAnonymous]
        public IActionResult GenerateQRCode(string bankName, string bankAccount, string accountHolderName, decimal amount, int? creditorId = null, int? userId = null, int? year = null, int? month = null)
        {
            if (string.IsNullOrEmpty(bankAccount) || string.IsNullOrEmpty(bankName))
            {
                return NotFound();
            }

            // Làm tròn số tiền lên (round up) để đảm bảo số tiền trong QR code khớp với số tiền khi thanh toán
            var roundedAmount = Math.Ceiling(amount);

            string? description = null;
            // Nếu có creditorId và userId, tạo description với format: {Prefix}-{encodedCreditorId}-{userId}-{year}-{month}[-{Suffix}]
            // Year và Month là bắt buộc khi có creditorId và userId
            if (creditorId.HasValue && userId.HasValue)
            {
                // Year và Month là bắt buộc khi generate QR code cho thanh toán
                if (!year.HasValue || !month.HasValue)
                {
                    return BadRequest(new { error = "Year and Month are required when generating QR code for payment" });
                }

                // Validate year và month
                if (year.Value < 2000 || year.Value > 2100)
                {
                    return BadRequest(new { error = "Year must be between 2000 and 2100" });
                }

                if (month.Value < 1 || month.Value > 12)
                {
                    return BadRequest(new { error = "Month must be between 1 and 12" });
                }

                description = IdEncoderHelper.CreatePaymentDescription(creditorId.Value, userId.Value, year.Value, month.Value);
            }

            var qrBytes = QRCodeHelper.GeneratePaymentQRCode(
                bankName,
                bankAccount,
                accountHolderName ?? "",
                roundedAmount,
                description
            );

            return File(qrBytes, "image/png", $"QRCode_{amount:N0}.png");
        }

        // POST: Payments/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? year = null, int? month = null, int? groupId = null, string? returnAction = null)
        {
            var monthlyPayment = await _context.MonthlyPayments.FindAsync(id);
            if (monthlyPayment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thanh toán";
                var redirectYear = year ?? DateTime.Now.Year;
                var redirectMonth = month ?? DateTime.Now.Month;
                // Nếu có returnAction, redirect về đó, nếu không thì về Index
                if (!string.IsNullOrEmpty(returnAction) && returnAction == "PendingPayments")
                {
                    return RedirectToAction("PendingPayments", "Reports", new { year = redirectYear, month = redirectMonth, groupId = groupId });
                }
                return RedirectToAction(nameof(Index), new { year = redirectYear, month = redirectMonth, groupId = groupId });
            }

            var currentUserId = SessionHelper.GetUserId(HttpContext);
            if (currentUserId == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa đăng nhập";
                return RedirectToAction("Login", "Account");
            }

            // Check permission
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                // SuperAdmin có thể xóa tất cả
            }
            else if (SessionHelper.IsAdmin(HttpContext))
            {
                // Admin chỉ xóa được payments trong nhóm của mình
                var adminGroupId = SessionHelper.GetGroupId(HttpContext);
                if (!adminGroupId.HasValue || monthlyPayment.GroupId != adminGroupId.Value)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập thanh toán này.";
                    var redirectYear = year ?? monthlyPayment.Year;
                    var redirectMonth = month ?? monthlyPayment.Month;
                    // Nếu có returnAction, redirect về đó, nếu không thì về Index
                    if (!string.IsNullOrEmpty(returnAction) && returnAction == "PendingPayments")
                    {
                        return RedirectToAction("PendingPayments", "Reports", new { year = redirectYear, month = redirectMonth, groupId = groupId });
                    }
                    return RedirectToAction(nameof(Index), new { year = redirectYear, month = redirectMonth, groupId = groupId });
                }
            }
            else if (SessionHelper.IsUser(HttpContext))
            {
                // User thường chỉ xóa được payments mà họ là creditor (người được nợ)
                var startDate = new DateTime(monthlyPayment.Year, monthlyPayment.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Kiểm tra xem có expense nào mà user là payer và monthlyPayment.UserId là participant không
                var isCreditor = await _context.Expenses
                    .Include(e => e.Participants)
                    .AnyAsync(e => e.PayerId == currentUserId.Value &&
                                  e.Participants.Any(p => p.UserId == monthlyPayment.UserId) &&
                                  e.ExpenseDate >= startDate && e.ExpenseDate <= endDate);

                if (!isCreditor)
                {
                    TempData["ErrorMessage"] = "Bạn chỉ có thể từ chối thanh toán từ những người nợ bạn";
                    var redirectYear = year ?? monthlyPayment.Year;
                    var redirectMonth = month ?? monthlyPayment.Month;
                    // Nếu có returnAction, redirect về đó, nếu không thì về Index
                    if (!string.IsNullOrEmpty(returnAction) && returnAction == "PendingPayments")
                    {
                        return RedirectToAction("PendingPayments", "Reports", new { year = redirectYear, month = redirectMonth, groupId = groupId });
                    }
                    return RedirectToAction(nameof(Index), new { year = redirectYear, month = redirectMonth, groupId = groupId });
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa thanh toán.";
                var redirectYear = year ?? DateTime.Now.Year;
                var redirectMonth = month ?? DateTime.Now.Month;
                // Nếu có returnAction, redirect về đó, nếu không thì về Index
                if (!string.IsNullOrEmpty(returnAction) && returnAction == "PendingPayments")
                {
                    return RedirectToAction("PendingPayments", "Reports", new { year = redirectYear, month = redirectMonth, groupId = groupId });
                }
                return RedirectToAction(nameof(Index), new { year = redirectYear, month = redirectMonth, groupId = groupId });
            }

            var finalYear = year ?? monthlyPayment.Year;
            var finalMonth = month ?? monthlyPayment.Month;
            _context.MonthlyPayments.Remove(monthlyPayment);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa thanh toán thành công!";

            // Nếu có returnAction, redirect về đó, nếu không thì về Index
            if (!string.IsNullOrEmpty(returnAction) && returnAction == "PendingPayments")
            {
                return RedirectToAction("PendingPayments", "Reports", new { year = finalYear, month = finalMonth, groupId = groupId });
            }
            return RedirectToAction(nameof(Index), new { year = finalYear, month = finalMonth, groupId = groupId });
        }

        private bool MonthlyPaymentExists(int id)
        {
            return _context.MonthlyPayments.Any(e => e.Id == id);
        }

        private async Task<MonthlyReportViewModel> GetMonthlyReportAsync(int year, int month, int? filterGroupId = null)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Filter by GroupId
            var expenseQuery = _context.Expenses
                .Include(e => e.Payer)
                .Include(e => e.Participants)
                    .ThenInclude(ep => ep.User)
                .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate)
                .AsQueryable();

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
            var paymentQuery = _context.MonthlyPayments
                .Include(mp => mp.User)
                .Where(mp => mp.Year == year && mp.Month == month && mp.Status == "Confirmed")
                .AsQueryable();

            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    expenseQuery = expenseQuery.Where(e => e.GroupId == currentGroupId.Value);
                    userQuery = userQuery.Where(u => u.GroupId == currentGroupId.Value);
                    paymentQuery = paymentQuery.Where(mp => mp.GroupId == currentGroupId.Value);
                }
                else
                {
                    expenseQuery = expenseQuery.Where(e => false);
                    userQuery = userQuery.Where(u => false);
                    paymentQuery = paymentQuery.Where(mp => false);
                }
            }
            else
            {
                // SuperAdmin có thể filter theo nhóm
                if (filterGroupId.HasValue)
                {
                    expenseQuery = expenseQuery.Where(e => e.GroupId == filterGroupId.Value);
                    userQuery = userQuery.Where(u => u.GroupId == filterGroupId.Value);
                    paymentQuery = paymentQuery.Where(mp => mp.GroupId == filterGroupId.Value);
                }
                // Nếu không chọn nhóm thì hiển thị tất cả
            }

            var expenses = await expenseQuery.ToListAsync();
            var users = await userQuery.OrderBy(u => u.Name).ToListAsync();
            var payments = await paymentQuery.ToListAsync();

            // Calculate user debts với logic mới
            var userDebts = new List<UserDebtDetail>();
            var allDebtDetails = new List<DebtDetail>(); // Tổng hợp tất cả các mối quan hệ nợ

            // BƯỚC 1: Tạo TẤT CẢ các DebtDetail trước
            foreach (var expense in expenses)
            {
                var payer = expense.Payer;
                if (payer == null)
                {
                    continue;
                }
                var payerId = payer.Id;
                var payerName = payer.Name;
                var payerAvatar = payer.AvatarPath;

                foreach (var participant in expense.Participants)
                {
                    // Nếu participant không phải payer thì nợ payer
                    if (participant.UserId != payerId)
                    {
                        var participantUser = participant.User;
                        if (participantUser == null)
                        {
                            continue;
                        }
                        var participantUserName = participantUser.Name;
                        var participantAvatar = participantUser.AvatarPath;

                        // Tính số tiền: nếu có Amount trong ExpenseParticipant thì dùng giá trị đó, nếu không thì chia đều
                        decimal amountPerPerson;
                        if (participant.Amount.HasValue)
                        {
                            amountPerPerson = participant.Amount.Value;
                        }
                        else
                        {
                            // Chia đều cho tất cả participants (chỉ tính những người không có Amount)
                            var participantsWithoutAmount = expense.Participants.Where(p => !p.Amount.HasValue).ToList();
                            var participantsWithoutAmountCount = participantsWithoutAmount.Count;
                            var totalCustomAmount = expense.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount ?? 0m);
                            var remainingAmount = expense.Amount - totalCustomAmount;
                            amountPerPerson = 0;
                            if (participantsWithoutAmountCount > 0)
                            {
                                amountPerPerson = Math.Round(remainingAmount / participantsWithoutAmountCount, 2);
                            }
                        }

                        // Kiểm tra xem đã có debt detail này chưa
                        var existingDebt = allDebtDetails.FirstOrDefault(d =>
                            d.DebtorId == participant.UserId &&
                            d.CreditorId == payerId &&
                            d.ExpenseId == expense.Id);

                        if (existingDebt == null)
                        {
                            var debtDetail = new DebtDetail
                            {
                                DebtorId = participant.UserId,
                                DebtorName = participantUserName,
                                DebtorAvatarPath = participantAvatar,
                                CreditorId = payerId,
                                CreditorName = payerName,
                                CreditorAvatarPath = payerAvatar,
                                Amount = amountPerPerson,
                                RemainingAmount = amountPerPerson, // Ban đầu RemainingAmount = Amount
                                ExpenseId = expense.Id,
                                ExpenseDate = expense.ExpenseDate,
                                Description = expense.Description
                            };
                            allDebtDetails.Add(debtDetail);
                        }
                    }
                }
            }

            // BƯỚC 2: Phân bổ thanh toán cho từng user và tính toán các thông tin khác
            foreach (var user in users)
            {
                var totalAmount = 0m; // Tổng phải trả (khi là participant)
                var paidAsPayer = 0m; // Tổng đã chi (khi là payer)
                var userPayments = payments.Where(p => p.UserId == user.Id).ToList();

                // Tính tổng phải trả (khi là participant)
                foreach (var expense in expenses)
                {
                    if (expense.Participants.Any(ep => ep.UserId == user.Id))
                    {
                        var participant = expense.Participants.FirstOrDefault(ep => ep.UserId == user.Id);
                        if (participant != null)
                        {
                            if (participant.Amount.HasValue)
                            {
                                // Dùng số tiền cụ thể từ ExpenseParticipant
                                totalAmount += participant.Amount.Value;
                            }
                            else
                            {
                                // Chia đều: tính số tiền còn lại sau khi trừ các custom amounts
                                var participantsWithoutAmount = expense.Participants.Where(p => !p.Amount.HasValue).ToList();
                                var participantsWithoutAmountCount = participantsWithoutAmount.Count;
                                var totalCustomAmount = expense.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount ?? 0m);
                                var remainingAmount = expense.Amount - totalCustomAmount;
                                var amountPerPerson = 0m;
                                if (participantsWithoutAmountCount > 0)
                                {
                                    amountPerPerson = Math.Round(remainingAmount / participantsWithoutAmountCount, 2);
                                }
                                totalAmount += amountPerPerson;
                            }
                        }
                    }
                }

                // Tính tổng đã chi (khi là payer)
                paidAsPayer = expenses.Where(e => e.PayerId == user.Id).Sum(e => e.Amount);

                // Lấy TẤT CẢ các khoản nợ của user này từ allDebtDetails
                var userDebtDetails = allDebtDetails
                    .Where(d => d.DebtorId == user.Id)
                    .OrderBy(d => d.ExpenseDate)
                    .ThenBy(d => d.ExpenseId)
                    .ToList();

                var paidAmount = userPayments.Sum(p => p.PaidAmount);

                // Phân bổ các khoản thanh toán vào các khoản nợ
                // Nếu payment có CreditorId, chỉ trừ vào debt có CreditorId tương ứng
                // Nếu payment không có CreditorId (payment cũ), áp dụng logic FIFO
                foreach (var payment in userPayments.OrderBy(p => p.PaidDate))
                {
                    if (payment.CreditorId.HasValue)
                    {
                        // Payment có CreditorId: chỉ trừ vào debt có CreditorId tương ứng
                        var targetDebts = userDebtDetails
                            .Where(d => d.CreditorId == payment.CreditorId.Value && d.RemainingAmount > 0)
                            .OrderBy(d => d.ExpenseDate)
                            .ThenBy(d => d.ExpenseId)
                            .ToList();

                        decimal remainingPayment = payment.PaidAmount;
                        foreach (var debt in targetDebts)
                        {
                            if (remainingPayment <= 0) break;

                            if (debt.RemainingAmount > 0)
                            {
                                var paymentApplied = Math.Min(debt.RemainingAmount, remainingPayment);
                                debt.RemainingAmount -= paymentApplied;
                                remainingPayment -= paymentApplied;
                            }
                        }
                    }
                    else
                    {
                        // Payment không có CreditorId (payment cũ): áp dụng logic FIFO
                        decimal remainingPayment = payment.PaidAmount;
                        foreach (var debt in userDebtDetails)
                        {
                            if (remainingPayment <= 0) break;

                            if (debt.RemainingAmount > 0)
                            {
                                var paymentApplied = Math.Min(debt.RemainingAmount, remainingPayment);
                                debt.RemainingAmount -= paymentApplied;
                                remainingPayment -= paymentApplied;
                            }
                        }
                    }
                }

                userDebts.Add(new UserDebtDetail
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    TotalAmount = totalAmount,
                    PaidAmount = paidAmount,
                    PaidAsPayer = paidAsPayer,
                    DebtDetails = userDebtDetails,
                    Payments = userPayments.Select(p => new PaymentDetail
                    {
                        Id = p.Id,
                        PaidAmount = p.PaidAmount,
                        PaidDate = p.PaidDate,
                        Notes = p.Notes,
                        UserId = p.UserId,
                        UserName = p.User.Name,
                        AvatarPath = p.User.AvatarPath
                    }).ToList(),
                    BankName = user.BankName,
                    BankAccount = user.BankAccount,
                    AccountHolderName = user.AccountHolderName,
                    AvatarPath = user.AvatarPath
                });
            }

            // Khấu trừ các khoản nợ hai chiều để lấy nợ thuần
            DebtHelper.NetMutualDebts(allDebtDetails);

            foreach (var debt in userDebts)
            {
                debt.DebtDetails = debt.DebtDetails
                    .Where(d => d.RemainingAmount > 0)
                    .OrderBy(d => d.ExpenseDate)
                    .ThenBy(d => d.ExpenseId)
                    .ToList();
            }

            var netDebtSummaries = allDebtDetails
                .Where(d => d.RemainingAmount > 0)
                .GroupBy(d => new { d.DebtorId, d.CreditorId })
                .Select(g =>
                {
                    var debtorUser = users.FirstOrDefault(u => u.Id == g.Key.DebtorId);
                    var creditorUser = users.FirstOrDefault(u => u.Id == g.Key.CreditorId);

                    return new NetDebtSummary
                    {
                        DebtorId = g.Key.DebtorId,
                        DebtorName = debtorUser?.Name ?? g.First().DebtorName,
                        DebtorAvatarPath = debtorUser?.AvatarPath ?? g.First().DebtorAvatarPath,
                        CreditorId = g.Key.CreditorId,
                        CreditorName = creditorUser?.Name ?? g.First().CreditorName,
                        CreditorAvatarPath = creditorUser?.AvatarPath ?? g.First().CreditorAvatarPath,
                        CreditorBankName = creditorUser?.BankName,
                        CreditorBankAccount = creditorUser?.BankAccount,
                        CreditorAccountHolderName = creditorUser?.AccountHolderName,
                        Amount = g.Sum(d => d.RemainingAmount)
                    };
                })
                .Where(nd => nd.Amount > 0)
                .OrderBy(nd => nd.DebtorName)
                .ThenBy(nd => nd.CreditorName)
                .ToList();

            // Build expense details
            var expenseDetails = expenses.Select(e => new ExpenseDetail
            {
                Id = e.Id,
                Amount = e.Amount,
                PayerId = e.PayerId,
                PayerName = e.Payer.Name,
                PayerAvatarPath = e.Payer.AvatarPath,
                ExpenseDate = e.ExpenseDate,
                Description = e.Description,
                ParticipantNames = e.Participants.Select(ep => ep.User.Name).ToList(),
                AmountPerPerson = e.Participants.Count > 0 ? e.Amount / e.Participants.Count : 0,
                ParticipantAmounts = e.Participants
                    .Where(p => p.Amount.HasValue)
                    .ToDictionary(p => p.UserId, p => p.Amount!.Value),
                ParticipantIdToName = e.Participants.ToDictionary(p => p.UserId, p => p.User.Name),
                ParticipantIdToAvatarPath = e.Participants.ToDictionary(p => p.UserId, p => p.User.AvatarPath)
            }).ToList();

            // Tập hợp nợ theo người được nợ (CreditorSummary) - chỉ cho người đang xem
            var creditorSummaries = new List<CreditorSummary>();
            var currentUserId = SessionHelper.GetUserId(HttpContext);

            if (currentUserId.HasValue)
            {
                // Lấy các khoản nợ của người đang xem
                var currentUserDebts = userDebts.FirstOrDefault(u => u.UserId == currentUserId.Value);
                if (currentUserDebts != null && currentUserDebts.DebtDetails.Any())
                {
                    // Chỉ lấy các khoản nợ còn lại (RemainingAmount > 0)
                    var remainingDebtDetails = currentUserDebts.DebtDetails
                        .Where(d => d.RemainingAmount > 0)
                        .ToList();

                    if (remainingDebtDetails.Any())
                    {
                        // Nhóm các khoản nợ theo CreditorId
                        var groupedByCreditor = remainingDebtDetails
                            .GroupBy(d => d.CreditorId)
                            .ToList();

                        foreach (var group in groupedByCreditor)
                        {
                            var creditorId = group.Key;
                            var creditor = users.FirstOrDefault(u => u.Id == creditorId);
                            if (creditor != null)
                            {
                                // Tính tổng số tiền còn nợ (RemainingAmount)
                                var totalAmount = group.Sum(d => d.RemainingAmount);
                                creditorSummaries.Add(new CreditorSummary
                                {
                                    CreditorId = creditorId,
                                    CreditorName = creditor.Name,
                                    TotalAmount = totalAmount,
                                    DebtDetails = group.ToList(),
                                    BankName = creditor.BankName,
                                    BankAccount = creditor.BankAccount,
                                    AccountHolderName = creditor.AccountHolderName
                                });
                            }
                        }
                    }
                }
            }

            return new MonthlyReportViewModel
            {
                Year = year,
                Month = month,
                TotalExpenses = expenses.Sum(e => e.Amount),
                TotalTransactions = expenses.Count,
                UserDebts = userDebts,
                Expenses = expenseDetails,
                CreditorSummaries = creditorSummaries,
                CurrentUserId = currentUserId,
                NetDebts = netDebtSummaries
            };
        }
    }
}

