using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using QuanLyAnTrua.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using System.Text;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
using Serilog;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // GET: Reports/ExportPdf
        public async Task<IActionResult> ExportPdf(int year, int month, int? userId = null)
        {
            var report = await GetMonthlyReportAsync(year, month, userId);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Text($"Báo cáo thanh toán tháng {month}/{year}")
                        .SemiBold().FontSize(16).AlignCenter();

                    page.Content()
                        .Column(column =>
                        {
                            column.Spacing(10);

                            // Tổng quan
                            column.Item().Text($"Tổng chi phí: {report.TotalExpenses:N0} đ").FontSize(12).SemiBold();
                            column.Item().Text($"Số giao dịch: {report.TotalTransactions}").FontSize(12);

                            column.Item().PaddingTop(10);

                            // Chi tiết nợ theo người dùng
                            column.Item().Text("Chi tiết nợ theo người dùng").FontSize(14).SemiBold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Người dùng").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Phải trả").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Đã chi").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Nợ thực tế").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Đã thanh toán").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Còn lại").SemiBold();
                                });

                                foreach (var userDebt in report.UserDebts.OrderBy(u => u.UserName))
                                {
                                    var userName = userDebt.UserName;
                                    if (!string.IsNullOrEmpty(userDebt.BankName) || !string.IsNullOrEmpty(userDebt.BankAccount))
                                    {
                                        userName += "\n" + (userDebt.BankName ?? "") + " " + (userDebt.BankAccount ?? "");
                                    }
                                    table.Cell().Element(CellStyle).Text(userName);
                                    table.Cell().Element(CellStyle).Text($"{userDebt.TotalAmount:N0} đ");
                                    table.Cell().Element(CellStyle).Text($"{userDebt.PaidAsPayer:N0} đ");
                                    table.Cell().Element(CellStyle).Text($"{userDebt.ActualDebt:N0} đ");
                                    table.Cell().Element(CellStyle).Text($"{userDebt.PaidAmount:N0} đ");
                                    table.Cell().Element(CellStyle).Text($"{userDebt.RemainingAmount:N0} đ");
                                }
                            });
                        });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"BaoCaoThanhToan_{year}_{month}.pdf");
        }

        static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(5)
                .AlignCenter()
                .AlignMiddle();
        }

        // GET: Reports/ExportExcel
        public async Task<IActionResult> ExportExcel(int year, int month, int? userId = null)
        {
            var report = await GetMonthlyReportAsync(year, month, userId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"Tháng {month}/{year}");

            // Header
            worksheet.Cell(1, 1).Value = $"Báo cáo thanh toán tháng {month}/{year}";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Range(1, 1, 1, 4).Merge();

            worksheet.Cell(3, 1).Value = "Tổng chi phí:";
            worksheet.Cell(3, 2).Value = report.TotalExpenses;
            worksheet.Cell(3, 2).Style.NumberFormat.Format = "#,##0";

            worksheet.Cell(4, 1).Value = "Số giao dịch:";
            worksheet.Cell(4, 2).Value = report.TotalTransactions;

            // User debts table
            var row = 6;
            worksheet.Cell(row, 1).Value = "Người dùng";
            worksheet.Cell(row, 2).Value = "Phải trả";
            worksheet.Cell(row, 3).Value = "Đã chi";
            worksheet.Cell(row, 4).Value = "Nợ thực tế";
            worksheet.Cell(row, 5).Value = "Đã thanh toán";
            worksheet.Cell(row, 6).Value = "Còn lại";
            worksheet.Cell(row, 7).Value = "Ngân hàng";
            worksheet.Cell(row, 8).Value = "Số TK";
            worksheet.Range(row, 1, row, 8).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.LightGray;

            row++;
            foreach (var userDebt in report.UserDebts.OrderBy(u => u.UserName))
            {
                worksheet.Cell(row, 1).Value = userDebt.UserName;
                worksheet.Cell(row, 2).Value = userDebt.TotalAmount;
                worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(row, 3).Value = userDebt.PaidAsPayer;
                worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(row, 4).Value = userDebt.ActualDebt;
                worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(row, 5).Value = userDebt.PaidAmount;
                worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(row, 6).Value = userDebt.RemainingAmount;
                worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(row, 7).Value = userDebt.BankName ?? "";
                worksheet.Cell(row, 8).Value = userDebt.BankAccount ?? "";
                row++;
            }

            // Chi tiết ai nợ ai
            if (report.UserDebts.Any(u => u.DebtDetails.Any()))
            {
                row += 2;
                worksheet.Cell(row, 1).Value = "Chi tiết ai nợ ai";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 14;
                worksheet.Range(row, 1, row, 6).Merge();

                row++;
                worksheet.Cell(row, 1).Value = "Người nợ";
                worksheet.Cell(row, 2).Value = "Người được nợ";
                worksheet.Cell(row, 3).Value = "Số tiền";
                worksheet.Cell(row, 4).Value = "Ngày";
                worksheet.Cell(row, 5).Value = "Mô tả";
                worksheet.Range(row, 1, row, 5).Style.Font.Bold = true;
                worksheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;

                row++;
                foreach (var debt in report.UserDebts)
                {
                    foreach (var debtDetail in debt.DebtDetails)
                    {
                        worksheet.Cell(row, 1).Value = debtDetail.DebtorName;
                        worksheet.Cell(row, 2).Value = debtDetail.CreditorName;
                        worksheet.Cell(row, 3).Value = debtDetail.Amount;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                        worksheet.Cell(row, 4).Value = debtDetail.ExpenseDate.ToString("dd/MM/yyyy");
                        worksheet.Cell(row, 5).Value = debtDetail.Description ?? "";
                        row++;
                    }
                }
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BaoCaoThanhToan_{year}_{month}.xlsx");
        }

        // GET: Reports/GenerateQRCode
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

        // GET: Reports/ShareLink
        public async Task<IActionResult> ShareLink(int? year, int? month, string reportType = "Group", int? userId = null, int? groupId = null)
        {
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            ViewBag.Year = currentYear;
            ViewBag.Month = currentMonth;
            ViewBag.ReportType = reportType;
            ViewBag.UserId = userId;
            ViewBag.GroupId = groupId;

            // Always load users for dropdown (will be shown/hidden by JS)
            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
            if (!SessionHelper.IsSuperAdmin(HttpContext))
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
            ViewBag.Users = await userQuery.OrderBy(u => u.Name).ToListAsync();

            // Always load groups for dropdown (will be shown/hidden by JS)
            var groupQuery = _context.Groups.Where(g => g.IsActive).AsQueryable();
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var currentGroupId = SessionHelper.GetGroupId(HttpContext);
                if (currentGroupId.HasValue)
                {
                    groupQuery = groupQuery.Where(g => g.Id == currentGroupId.Value);
                }
                else
                {
                    groupQuery = groupQuery.Where(g => false);
                }
            }
            ViewBag.Groups = await groupQuery.OrderBy(g => g.Name).ToListAsync();

            return View();
        }

        // POST: Reports/ShareLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareLink(string reportType, int? userId, int? groupId, DateTime? expiresAt)
        {
            var currentUserId = SessionHelper.GetUserId(HttpContext);
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Validate permissions
            if (reportType == "User" && userId.HasValue)
            {
                if (!SessionHelper.IsSuperAdmin(HttpContext))
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId.Value);
                    if (currentUser == null)
                    {
                        return RedirectToAction("Login", "Account");
                    }
                    // User chỉ có thể tạo link cho chính mình
                    if (currentUser.Role == "User" && userId.Value != currentUserId.Value)
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ có thể tạo link cho chính mình";
                        return RedirectToAction("Index", "Payments");
                    }
                    // Admin chỉ có thể tạo link cho users trong nhóm
                    if (currentUser.Role == "Admin")
                    {
                        var targetUser = await _context.Users.FindAsync(userId.Value);
                        if (targetUser?.GroupId != currentUser.GroupId)
                        {
                            TempData["ErrorMessage"] = "Bạn chỉ có thể tạo link cho users trong nhóm của mình";
                            return RedirectToAction("Index", "Payments");
                        }
                    }
                }
            }
            else if (reportType == "Group" && groupId.HasValue)
            {
                if (!SessionHelper.IsSuperAdmin(HttpContext))
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId.Value);
                    if (currentUser == null)
                    {
                        return RedirectToAction("Login", "Account");
                    }
                    if (currentUser.GroupId != groupId.Value)
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ có thể tạo link cho nhóm của mình";
                        return RedirectToAction("Index", "Payments");
                    }
                }
            }

            // Validate expiresAt - phải là thời gian trong tương lai
            if (expiresAt.HasValue && expiresAt.Value <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Thời gian hết hạn phải là thời gian trong tương lai";
                return RedirectToAction("ShareLink", new { year = DateTime.Now.Year, month = DateTime.Now.Month, reportType, userId, groupId });
            }

            // Generate unique token
            string token;
            do
            {
                token = TokenHelper.GenerateSecureToken(32);
            } while (await _context.SharedReports.AnyAsync(sr => sr.Token == token));

            var sharedReport = new SharedReport
            {
                Token = token,
                ReportType = reportType,
                UserId = reportType == "User" ? userId : null,
                GroupId = reportType == "Group" ? groupId : null,
                CreatedBy = currentUserId.Value,
                CreatedAt = DateTime.Now,
                ExpiresAt = expiresAt,
                IsActive = true
            };

            _context.Add(sharedReport);
            await _context.SaveChangesAsync();

            var shareUrl = Url.Action("PublicView", "Reports", new { token = token }, Request.Scheme);
            TempData["ShareUrl"] = shareUrl;
            TempData["SuccessMessage"] = "Tạo link chia sẻ thành công!";

            return RedirectToAction("ShareLink", new { year = DateTime.Now.Year, month = DateTime.Now.Month, reportType, userId, groupId });
        }

        // GET: Reports/PublicView/{token}
        [AllowAnonymous]
        public async Task<IActionResult> PublicView(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return NotFound();
            }

            var sharedReport = await _context.SharedReports
                .Include(sr => sr.User)
                .Include(sr => sr.Group)
                .FirstOrDefaultAsync(sr => sr.Token == token);

            if (sharedReport == null || !sharedReport.IsActive)
            {
                return NotFound();
            }

            // Check expire date
            if (sharedReport.ExpiresAt.HasValue && sharedReport.ExpiresAt.Value < DateTime.Now)
            {
                return View("Expired");
            }

            // Update last accessed
            sharedReport.LastAccessedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Get report data
            int? userId = null;
            int? groupId = null;

            if (sharedReport.ReportType == "User" && sharedReport.UserId.HasValue)
            {
                userId = sharedReport.UserId.Value;
            }
            else if (sharedReport.ReportType == "Group" && sharedReport.GroupId.HasValue)
            {
                groupId = sharedReport.GroupId.Value;
            }

            // Lấy tháng/năm từ SharedReport (tháng mà report được tạo)
            var reportYear = sharedReport.CreatedAt.Year;
            var reportMonth = sharedReport.CreatedAt.Month;

            // Sử dụng method riêng cho PublicView: chi phí chỉ trong tháng, nợ từ tất cả các tháng
            var report = await GetPublicViewReportAsync(reportYear, reportMonth, userId, groupId);

            ViewBag.Token = token;
            ViewBag.ReportType = sharedReport.ReportType;
            ViewBag.ExpiresAt = sharedReport.ExpiresAt;

            return View(report);
        }

        // POST: Reports/CreatePendingPayment
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> CreatePendingPayment(string token, int userId, decimal amount, int creditorId, string? notes = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Json(new { success = false, message = "Token không hợp lệ" });
            }

            var sharedReport = await _context.SharedReports
                .FirstOrDefaultAsync(sr => sr.Token == token && sr.IsActive);

            if (sharedReport == null)
            {
                return Json(new { success = false, message = "Link không hợp lệ hoặc đã hết hạn" });
            }

            // Check expire date
            if (sharedReport.ExpiresAt.HasValue && sharedReport.ExpiresAt.Value < DateTime.Now)
            {
                return Json(new { success = false, message = "Link đã hết hạn" });
            }

            // Validate user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Json(new { success = false, message = "Người dùng không hợp lệ" });
            }

            // Validate creditor exists
            var creditor = await _context.Users.FindAsync(creditorId);
            if (creditor == null || !creditor.IsActive)
            {
                return Json(new { success = false, message = "Người được thanh toán không hợp lệ" });
            }

            // Get current year and month
            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            // Kiểm tra xem đã có payment pending cho cùng userId + creditorId + tháng/năm chưa
            var existingPendingPayment = await _context.MonthlyPayments
                .FirstOrDefaultAsync(mp =>
                    mp.UserId == userId &&
                    mp.CreditorId == creditorId &&
                    mp.Year == currentYear &&
                    mp.Month == currentMonth &&
                    mp.Status == "Pending");

            if (existingPendingPayment != null)
            {
                return Json(new
                {
                    success = false,
                    message = $"Bạn đã có một yêu cầu thanh toán đang chờ xác nhận cho {creditor.Name}. Vui lòng chờ xác nhận hoặc liên hệ admin để hủy yêu cầu cũ."
                });
            }

            // Làm tròn số tiền lên (round up) để khớp với số tiền trong QR code
            var roundedAmount = Math.Ceiling(amount);

            // Set GroupId
            int? groupId = null;
            if (user.GroupId.HasValue)
            {
                groupId = user.GroupId;
            }

            // Tạo notes: "Thanh toán cho {tên user}" + nội dung chuyển khoản (nếu có)
            var paymentNotes = new List<string>();
            paymentNotes.Add($"Thanh toán cho {creditor.Name}");

            // Thêm nội dung chuyển khoản từ notes parameter (nếu có) để đối soát
            if (!string.IsNullOrEmpty(notes))
            {
                // Nếu notes không chỉ là "Thanh toán cho {tên}", thêm vào
                if (notes != $"Thanh toán cho {creditor.Name}")
                {
                    paymentNotes.Add($"Nội dung CK: {notes}");
                }
            }

            // Create pending payment
            var monthlyPayment = new MonthlyPayment
            {
                UserId = userId,
                CreditorId = creditorId,
                Year = currentYear,
                Month = currentMonth,
                PaidAmount = roundedAmount, // Sử dụng số tiền đã làm tròn lên
                PaidDate = DateTime.Today,
                Notes = string.Join(" | ", paymentNotes),
                GroupId = groupId,
                Status = "Pending"
            };

            _context.Add(monthlyPayment);
            await _context.SaveChangesAsync();

            // Gửi thông báo Telegram cho người được trả tiền (creditor)
            try
            {
                if (!string.IsNullOrEmpty(creditor.TelegramUserId))
                {
                    var message = $"💳 Thông báo thanh toán mới\n\n" +
                                 $"👤 Người thanh toán: {user.Name}\n" +
                                 $"💵 Số tiền: {roundedAmount:N0} đ\n" +
                                 $"📅 Ngày: {DateTime.Now:dd/MM/yyyy HH:mm}\n" +
                                 $"📝 Ghi chú: {string.Join(" | ", paymentNotes)}\n\n" +
                                 $"⏳ Trạng thái: Chờ xác nhận\n\n" +
                                 $"Vui lòng kiểm tra và xác nhận thanh toán này trong hệ thống.";

                    var sent = await TelegramHelper.SendMessageAsync(creditor.TelegramUserId, message, null);
                    if (sent)
                    {
                        Log.Information("Đã gửi Telegram notification cho creditor {CreditorId} ({CreditorName}) về payment {PaymentId} từ user {UserId} ({UserName})",
                            creditor.Id, creditor.Name, monthlyPayment.Id, user.Id, user.Name);
                    }
                    else
                    {
                        Log.Warning("Không thể gửi Telegram notification cho creditor {CreditorId} về payment {PaymentId}",
                            creditor.Id, monthlyPayment.Id);
                    }
                }
                else
                {
                    Log.Information("Creditor {CreditorId} ({CreditorName}) chưa có TelegramUserId, bỏ qua gửi thông báo",
                        creditor.Id, creditor.Name);
                }
            }
            catch (Exception ex)
            {
                // Không throw exception để không ảnh hưởng đến response cho user
                // Chỉ log lỗi
                Log.Error(ex, "Lỗi khi gửi Telegram notification cho creditor {CreditorId} về payment {PaymentId}",
                    creditor.Id, monthlyPayment.Id);
            }

            return Json(new { success = true, message = "Đã gửi yêu cầu thanh toán. Vui lòng chờ xác nhận.", paymentId = monthlyPayment.Id });
        }

        // GET: Reports/CheckUpdates
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> CheckUpdates(string token, int? lastPaymentId = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Json(new { hasUpdate = false });
            }

            var sharedReport = await _context.SharedReports
                .FirstOrDefaultAsync(sr => sr.Token == token && sr.IsActive);

            if (sharedReport == null)
            {
                return Json(new { hasUpdate = false });
            }

            // Check expire date
            if (sharedReport.ExpiresAt.HasValue && sharedReport.ExpiresAt.Value < DateTime.Now)
            {
                return Json(new { hasUpdate = false });
            }

            // Xác định user nào cần check
            List<int> userIdsToCheck = new List<int>();

            if (sharedReport.ReportType == "User" && sharedReport.UserId.HasValue)
            {
                userIdsToCheck.Add(sharedReport.UserId.Value);
            }
            else if (sharedReport.ReportType == "Group" && sharedReport.GroupId.HasValue)
            {
                // Lấy tất cả users trong group
                var usersInGroup = await _context.Users
                    .Where(u => u.GroupId == sharedReport.GroupId.Value && u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();
                userIdsToCheck.AddRange(usersInGroup);
            }

            if (!userIdsToCheck.Any())
            {
                return Json(new { hasUpdate = false });
            }

            // Kiểm tra xem có payment mới nào cho users trong link chia sẻ không
            // Chỉ check payment có status "Confirmed" (đã xác nhận)
            // Nếu có lastPaymentId, chỉ check payment có Id lớn hơn
            // Nếu không có, check payment trong 5 phút gần đây
            bool hasNewPayment;
            if (lastPaymentId.HasValue)
            {
                hasNewPayment = await _context.MonthlyPayments
                    .AnyAsync(mp => userIdsToCheck.Contains(mp.UserId) &&
                                   mp.Id > lastPaymentId.Value &&
                                   mp.Status == "Confirmed");
            }
            else
            {
                // Check payment trong 5 phút gần đây và có status "Confirmed"
                var checkTime = DateTime.Now.AddMinutes(-5);
                hasNewPayment = await _context.MonthlyPayments
                    .AnyAsync(mp => userIdsToCheck.Contains(mp.UserId) &&
                                   mp.PaidDate >= checkTime &&
                                   mp.Status == "Confirmed");
            }

            return Json(new { hasUpdate = hasNewPayment });
        }

        // POST: Reports/ConfirmPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id, int? year = null, int? month = null, int? groupId = null)
        {
            var payment = await _context.MonthlyPayments.FindAsync(id);
            if (payment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thanh toán";
                var redirectYear = year ?? DateTime.Now.Year;
                var redirectMonth = month ?? DateTime.Now.Month;
                return RedirectToAction(nameof(PendingPayments), new { year = redirectYear, month = redirectMonth, groupId = groupId });
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
                // SuperAdmin có thể xác nhận tất cả
            }
            else if (SessionHelper.IsAdmin(HttpContext))
            {
                // Admin chỉ xác nhận được thanh toán trong nhóm của mình
                var currentUser = await _context.Users.FindAsync(currentUserId.Value);
                if (currentUser == null || payment.GroupId != currentUser.GroupId)
                {
                    TempData["ErrorMessage"] = "Bạn chỉ có thể xác nhận thanh toán trong nhóm của mình";
                    var redirectYear = year ?? DateTime.Now.Year;
                    var redirectMonth = month ?? DateTime.Now.Month;
                    return RedirectToAction(nameof(PendingPayments), new { year = redirectYear, month = redirectMonth, groupId = groupId });
                }
            }
            else if (SessionHelper.IsUser(HttpContext))
            {
                // User thường chỉ xác nhận được payments mà họ là creditor (người được nợ)
                // Kiểm tra xem user có phải là creditor của payment này không
                var startDate = new DateTime(payment.Year, payment.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Kiểm tra xem có expense nào mà user là payer và payment.UserId là participant không
                var isCreditor = await _context.Expenses
                    .Include(e => e.Participants)
                    .AnyAsync(e => e.PayerId == currentUserId.Value &&
                                  e.Participants.Any(p => p.UserId == payment.UserId) &&
                                  e.ExpenseDate >= startDate && e.ExpenseDate <= endDate);

                if (!isCreditor)
                {
                    TempData["ErrorMessage"] = "Bạn chỉ có thể xác nhận thanh toán từ những người nợ bạn";
                    var redirectYear = year ?? DateTime.Now.Year;
                    var redirectMonth = month ?? DateTime.Now.Month;
                    return RedirectToAction(nameof(PendingPayments), new { year = redirectYear, month = redirectMonth, groupId = groupId });
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xác nhận thanh toán";
                var redirectYear = year ?? DateTime.Now.Year;
                var redirectMonth = month ?? DateTime.Now.Month;
                return RedirectToAction(nameof(PendingPayments), new { year = redirectYear, month = redirectMonth, groupId = groupId });
            }

            payment.Status = "Confirmed";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xác nhận thanh toán thành công";

            var finalYear = year ?? payment.Year;
            var finalMonth = month ?? payment.Month;
            return RedirectToAction(nameof(PendingPayments), new { year = finalYear, month = finalMonth, groupId = groupId });
        }

        // GET: Reports/PendingPayments
        public async Task<IActionResult> PendingPayments(int? year, int? month, int? groupId = null)
        {
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;
            var currentUserId = SessionHelper.GetUserId(HttpContext);

            // Load groups cho dropdown (chỉ SuperAdmin)
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                ViewBag.Groups = await _context.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                ViewBag.SelectedGroupId = groupId;
            }

            var query = _context.MonthlyPayments
                .Include(mp => mp.User)
                .Where(mp => mp.Status == "Pending" && mp.Year == currentYear && mp.Month == currentMonth)
                .AsQueryable();

            // Filter by permissions
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                // SuperAdmin có thể filter theo nhóm
                if (groupId.HasValue)
                {
                    query = query.Where(mp => mp.GroupId == groupId.Value);
                }
                // Nếu không chọn nhóm thì hiển thị tất cả
            }
            else if (SessionHelper.IsAdmin(HttpContext))
            {
                // Admin chỉ thấy payments trong nhóm của mình
                var adminGroupId = SessionHelper.GetGroupId(HttpContext);
                if (adminGroupId.HasValue)
                {
                    query = query.Where(mp => mp.GroupId == adminGroupId.Value);
                }
                else
                {
                    query = query.Where(mp => false);
                }
            }
            else if (SessionHelper.IsUser(HttpContext) && currentUserId.HasValue)
            {
                // User thường chỉ thấy payments mà họ là creditor (người được nợ)
                // Lọc theo CreditorId: nếu payment có CreditorId thì dùng CreditorId, nếu không thì fallback về expense
                var startDate = new DateTime(currentYear, currentMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Lấy tất cả payments trong tháng đó
                var allPayments = await query.ToListAsync();

                // Lọc payments: chỉ lấy những payment mà user là creditor
                var validPaymentIds = new List<int>();
                foreach (var payment in allPayments)
                {
                    bool isForCurrentUser = false;

                    // Nếu payment có CreditorId, kiểm tra xem có phải user hiện tại không
                    if (payment.CreditorId.HasValue)
                    {
                        isForCurrentUser = payment.CreditorId.Value == currentUserId.Value;
                    }
                    else
                    {
                        // Nếu không có CreditorId, fallback về kiểm tra expense (cho các payment cũ)
                        var isCreditor = await _context.Expenses
                            .Include(e => e.Participants)
                            .AnyAsync(e => e.PayerId == currentUserId.Value &&
                                          e.Participants.Any(p => p.UserId == payment.UserId && p.UserId != currentUserId.Value) &&
                                          e.ExpenseDate >= startDate && e.ExpenseDate <= endDate);

                        if (isCreditor)
                        {
                            isForCurrentUser = true;
                        }
                    }

                    if (isForCurrentUser)
                    {
                        validPaymentIds.Add(payment.Id);
                    }
                }

                if (validPaymentIds.Any())
                {
                    query = query.Where(mp => validPaymentIds.Contains(mp.Id));
                }
                else
                {
                    query = query.Where(mp => false);
                }
            }
            else
            {
                query = query.Where(mp => false);
            }

            var pendingPayments = await query.OrderByDescending(mp => mp.PaidDate).ToListAsync();

            ViewBag.Year = currentYear;
            ViewBag.Month = currentMonth;

            return View(pendingPayments);
        }

        // GET: Reports/SharedLinks
        public async Task<IActionResult> SharedLinks(int? month = null, int? year = null, int? groupId = null)
        {
            var userId = SessionHelper.GetUserId(HttpContext);
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.SharedReports
                .Include(sr => sr.User)
                .Include(sr => sr.Group)
                .Include(sr => sr.Creator)
                .AsQueryable();

            // SuperAdmin có thể filter theo nhóm
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                if (groupId.HasValue)
                {
                    // Lọc theo nhóm: links của nhóm hoặc links của user thuộc nhóm đó
                    query = query.Where(sr => (sr.GroupId == groupId.Value) || (sr.UserId.HasValue && sr.User != null && sr.User.GroupId == groupId.Value));
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
                // Filter by permissions
                var currentUser = await _context.Users.FindAsync(userId.Value);
                if (currentUser?.Role == "Admin")
                {
                    // Admin chỉ thấy links của nhóm mình
                    var currentGroupId = currentUser.GroupId;
                    query = query.Where(sr => (sr.GroupId == currentGroupId) || (sr.UserId.HasValue && sr.User != null && sr.User.GroupId == currentGroupId));
                }
                else if (currentUser?.Role == "User")
                {
                    // User chỉ thấy links của chính mình
                    query = query.Where(sr => sr.ReportType == "User" && sr.UserId == userId.Value);
                }
                else
                {
                    query = query.Where(sr => false);
                }
            }

            // Lọc theo tháng/năm (dựa vào CreatedAt)
            if (year.HasValue)
            {
                if (month.HasValue)
                {
                    // Lọc theo tháng và năm cụ thể
                    query = query.Where(sr => sr.CreatedAt.Year == year.Value && sr.CreatedAt.Month == month.Value);
                }
                else
                {
                    // Chỉ lọc theo năm
                    query = query.Where(sr => sr.CreatedAt.Year == year.Value);
                }
            }
            else if (month.HasValue)
            {
                // Nếu chỉ có tháng mà không có năm, lọc theo tháng của năm hiện tại
                var currentYear = DateTime.Now.Year;
                query = query.Where(sr => sr.CreatedAt.Year == currentYear && sr.CreatedAt.Month == month.Value);
            }

            var sharedReports = await query.OrderByDescending(sr => sr.CreatedAt).ToListAsync();

            // Lưu giá trị filter vào ViewBag để hiển thị trong dropdown
            ViewBag.SelectedMonth = month;
            ViewBag.SelectedYear = year;

            return View(sharedReports);
        }

        // POST: Reports/DeleteSharedLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSharedLink(int id)
        {
            var sharedReport = await _context.SharedReports.FindAsync(id);
            if (sharedReport == null)
            {
                return NotFound();
            }

            // Check permission
            var userId = SessionHelper.GetUserId(HttpContext);
            if (!SessionHelper.IsSuperAdmin(HttpContext) && sharedReport.CreatedBy != userId)
            {
                return Forbid();
            }

            _context.SharedReports.Remove(sharedReport);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa link chia sẻ thành công!";
            return RedirectToAction(nameof(SharedLinks));
        }

        // POST: Reports/ToggleSharedLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSharedLink(int id)
        {
            var sharedReport = await _context.SharedReports.FindAsync(id);
            if (sharedReport == null)
            {
                return NotFound();
            }

            // Check permission
            var userId = SessionHelper.GetUserId(HttpContext);
            if (!SessionHelper.IsSuperAdmin(HttpContext) && sharedReport.CreatedBy != userId)
            {
                return Forbid();
            }

            sharedReport.IsActive = !sharedReport.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã {(sharedReport.IsActive ? "kích hoạt" : "vô hiệu hóa")} link chia sẻ!";
            return RedirectToAction(nameof(SharedLinks));
        }

        private async Task<MonthlyReportViewModel> GetMonthlyReportAsync(int year, int month, int? userId = null, int? groupId = null)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Filter by GroupId or UserId
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

            if (userId.HasValue)
            {
                // Filter for specific user
                expenseQuery = expenseQuery.Where(e => e.GroupId.HasValue && e.Participants.Any(p => p.UserId == userId.Value));
                userQuery = userQuery.Where(u => u.Id == userId.Value);
                paymentQuery = paymentQuery.Where(mp => mp.UserId == userId.Value);
            }
            else if (groupId.HasValue)
            {
                // Filter for specific group
                expenseQuery = expenseQuery.Where(e => e.GroupId == groupId.Value);
                userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                paymentQuery = paymentQuery.Where(mp => mp.GroupId == groupId.Value);
            }
            else
            {
                // No filter - get all (for SuperAdmin or public view)
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
                var participantCount = expense.Participants.Count;

                foreach (var participant in expense.Participants)
                {
                    // Nếu participant không phải payer thì nợ payer
                    if (participant.UserId != payer.Id)
                    {
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
                            var totalCustomAmount = expense.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount!.Value);
                            var remainingAmount = expense.Amount - totalCustomAmount;
                            amountPerPerson = participantsWithoutAmount.Count > 0
                                ? Math.Round(remainingAmount / participantsWithoutAmount.Count, 2)
                                : 0;
                        }

                        // Kiểm tra xem đã có debt detail này chưa
                        var existingDebt = allDebtDetails.FirstOrDefault(d =>
                            d.DebtorId == participant.UserId &&
                            d.CreditorId == payer.Id &&
                            d.ExpenseId == expense.Id);

                        if (existingDebt == null)
                        {
                            var debtDetail = new DebtDetail
                            {
                                DebtorId = participant.UserId,
                                DebtorName = participant.User.Name,
                                CreditorId = payer.Id,
                                CreditorName = payer.Name,
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
                                var totalCustomAmount = expense.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount!.Value);
                                var remainingAmount = expense.Amount - totalCustomAmount;
                                var amountPerPerson = participantsWithoutAmount.Count > 0
                                    ? Math.Round(remainingAmount / participantsWithoutAmount.Count, 2)
                                    : 0;
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

                if (totalAmount > 0 || paidAmount > 0 || paidAsPayer > 0)
                {
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
                            UserName = p.User.Name
                        }).ToList(),
                        BankName = user.BankName,
                        BankAccount = user.BankAccount,
                        AccountHolderName = user.AccountHolderName
                    });
                }
            }

            // Prepare expense details
            var expenseDetails = expenses.Select(e => new ExpenseDetail
            {
                Id = e.Id,
                ExpenseDate = e.ExpenseDate,
                Amount = e.Amount,
                PayerName = e.Payer.Name,
                ParticipantNames = e.Participants.Select(p => p.User.Name).ToList(),
                AmountPerPerson = 0, // Sẽ được tính lại trong view dựa trên custom amounts
                Description = e.Description,
                // Lưu thông tin về custom amounts để hiển thị trong view
                // Dictionary: UserId -> Amount
                ParticipantAmounts = e.Participants
                    .Where(p => p.Amount.HasValue)
                    .ToDictionary(p => p.UserId, p => p.Amount!.Value),
                // Lưu mapping UserId -> UserName để dễ tra cứu trong view
                ParticipantIdToName = e.Participants.ToDictionary(p => p.UserId, p => p.User.Name)
            }).ToList();

            // Tập hợp nợ theo người được nợ (CreditorSummary) - chỉ cho người đang xem
            var creditorSummaries = new List<CreditorSummary>();

            // Nếu có userId (xem báo cáo của một người cụ thể), lấy nợ của người đó
            if (userId.HasValue)
            {
                var currentUserDebts = userDebts.FirstOrDefault(u => u.UserId == userId.Value);
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
                CurrentUserId = userId
            };
        }

        /// <summary>
        /// Lấy báo cáo cho PublicView: chi phí chỉ trong tháng, nợ và thanh toán từ tất cả các tháng
        /// </summary>
        private async Task<MonthlyReportViewModel> GetPublicViewReportAsync(int year, int month, int? userId = null, int? groupId = null)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // CHI PHÍ: Chỉ lấy trong tháng của SharedReport
            var expenseQuery = _context.Expenses
                .Include(e => e.Payer)
                .Include(e => e.Participants)
                    .ThenInclude(ep => ep.User)
                .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate)
                .AsQueryable();

            // NỢ: Tính từ TẤT CẢ các chi phí từ trước đến giờ (không giới hạn tháng)
            var allExpensesForDebtQuery = _context.Expenses
                .Include(e => e.Payer)
                .Include(e => e.Participants)
                    .ThenInclude(ep => ep.User)
                .AsQueryable();

            // THANH TOÁN: Lấy TẤT CẢ các thanh toán từ trước đến giờ (không giới hạn tháng/năm)
            var paymentQuery = _context.MonthlyPayments
                .Include(mp => mp.User)
                .Where(mp => mp.Status == "Confirmed")
                .AsQueryable();

            var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();

            // Filter by userId or groupId
            if (userId.HasValue)
            {
                expenseQuery = expenseQuery.Where(e => e.GroupId.HasValue && e.Participants.Any(p => p.UserId == userId.Value));
                allExpensesForDebtQuery = allExpensesForDebtQuery.Where(e => e.GroupId.HasValue && e.Participants.Any(p => p.UserId == userId.Value));
                userQuery = userQuery.Where(u => u.Id == userId.Value);
                paymentQuery = paymentQuery.Where(mp => mp.UserId == userId.Value);
            }
            else if (groupId.HasValue)
            {
                expenseQuery = expenseQuery.Where(e => e.GroupId == groupId.Value);
                allExpensesForDebtQuery = allExpensesForDebtQuery.Where(e => e.GroupId == groupId.Value);
                userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                paymentQuery = paymentQuery.Where(mp => mp.GroupId == groupId.Value);
            }

            var expenses = await expenseQuery.ToListAsync(); // Chỉ chi phí trong tháng
            var allExpensesForDebt = await allExpensesForDebtQuery.ToListAsync(); // Tất cả chi phí để tính nợ
            var users = await userQuery.OrderBy(u => u.Name).ToListAsync();
            var payments = await paymentQuery.ToListAsync(); // Tất cả thanh toán

            // Calculate user debts từ TẤT CẢ các chi phí (không chỉ trong tháng)
            var userDebts = new List<UserDebtDetail>();
            var allDebtDetails = new List<DebtDetail>();

            // BƯỚC 1: Tạo TẤT CẢ các DebtDetail từ tất cả chi phí
            foreach (var expense in allExpensesForDebt)
            {
                var payer = expense.Payer;
                var participantCount = expense.Participants.Count;

                foreach (var participant in expense.Participants)
                {
                    if (participant.UserId != payer.Id)
                    {
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
                            var totalCustomAmount = expense.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount!.Value);
                            var remainingAmount = expense.Amount - totalCustomAmount;
                            amountPerPerson = participantsWithoutAmount.Count > 0
                                ? Math.Round(remainingAmount / participantsWithoutAmount.Count, 2)
                                : 0;
                        }

                        var existingDebt = allDebtDetails.FirstOrDefault(d =>
                            d.DebtorId == participant.UserId &&
                            d.CreditorId == payer.Id &&
                            d.ExpenseId == expense.Id);

                        if (existingDebt == null)
                        {
                            var debtDetail = new DebtDetail
                            {
                                DebtorId = participant.UserId,
                                DebtorName = participant.User.Name,
                                CreditorId = payer.Id,
                                CreditorName = payer.Name,
                                Amount = amountPerPerson,
                                RemainingAmount = amountPerPerson,
                                ExpenseId = expense.Id,
                                ExpenseDate = expense.ExpenseDate,
                                Description = expense.Description
                            };
                            allDebtDetails.Add(debtDetail);
                        }
                    }
                }
            }

            // BƯỚC 2: Phân bổ thanh toán cho từng user
            foreach (var user in users)
            {
                var totalAmount = 0m;
                var paidAsPayer = 0m;
                var userPayments = payments.Where(p => p.UserId == user.Id).ToList();

                // Tính tổng phải trả từ TẤT CẢ chi phí
                foreach (var expense in allExpensesForDebt)
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
                                var totalCustomAmount = expense.Participants.Where(p => p.Amount.HasValue).Sum(p => p.Amount!.Value);
                                var remainingAmount = expense.Amount - totalCustomAmount;
                                var amountPerPerson = participantsWithoutAmount.Count > 0
                                    ? Math.Round(remainingAmount / participantsWithoutAmount.Count, 2)
                                    : 0;
                                totalAmount += amountPerPerson;
                            }
                        }
                    }
                }

                // Tính tổng đã chi từ TẤT CẢ chi phí
                paidAsPayer = allExpensesForDebt.Where(e => e.PayerId == user.Id).Sum(e => e.Amount);

                var userDebtDetails = allDebtDetails
                    .Where(d => d.DebtorId == user.Id)
                    .OrderBy(d => d.ExpenseDate)
                    .ThenBy(d => d.ExpenseId)
                    .ToList();

                var paidAmount = userPayments.Sum(p => p.PaidAmount);

                // Phân bổ thanh toán
                foreach (var payment in userPayments.OrderBy(p => p.PaidDate))
                {
                    if (payment.CreditorId.HasValue)
                    {
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

                if (totalAmount > 0 || paidAmount > 0 || paidAsPayer > 0)
                {
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
                            UserName = p.User.Name
                        }).ToList(),
                        BankName = user.BankName,
                        BankAccount = user.BankAccount,
                        AccountHolderName = user.AccountHolderName
                    });
                }
            }

            // Prepare expense details (chỉ chi phí trong tháng)
            var expenseDetails = expenses.Select(e => new ExpenseDetail
            {
                Id = e.Id,
                ExpenseDate = e.ExpenseDate,
                Amount = e.Amount,
                PayerName = e.Payer.Name,
                ParticipantNames = e.Participants.Select(p => p.User.Name).ToList(),
                AmountPerPerson = 0, // Sẽ được tính lại trong view dựa trên custom amounts
                Description = e.Description,
                // Lưu thông tin về custom amounts để hiển thị trong view
                ParticipantAmounts = e.Participants
                    .Where(p => p.Amount.HasValue)
                    .ToDictionary(p => p.UserId, p => p.Amount!.Value),
                // Lưu mapping UserId -> UserName để dễ tra cứu trong view
                ParticipantIdToName = e.Participants.ToDictionary(p => p.UserId, p => p.User.Name)
            }).ToList();

            // Tập hợp nợ theo người được nợ
            var creditorSummaries = new List<CreditorSummary>();

            if (userId.HasValue)
            {
                var currentUserDebts = userDebts.FirstOrDefault(u => u.UserId == userId.Value);
                if (currentUserDebts != null && currentUserDebts.DebtDetails.Any())
                {
                    var remainingDebtDetails = currentUserDebts.DebtDetails
                        .Where(d => d.RemainingAmount > 0)
                        .ToList();

                    if (remainingDebtDetails.Any())
                    {
                        var groupedByCreditor = remainingDebtDetails
                            .GroupBy(d => d.CreditorId)
                            .ToList();

                        foreach (var group in groupedByCreditor)
                        {
                            var creditorId = group.Key;
                            var creditor = users.FirstOrDefault(u => u.Id == creditorId);
                            if (creditor != null)
                            {
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
                TotalExpenses = expenses.Sum(e => e.Amount), // Chỉ tổng chi phí trong tháng
                TotalTransactions = expenses.Count, // Chỉ số lượng chi phí trong tháng
                UserDebts = userDebts, // Nợ từ tất cả các tháng
                Expenses = expenseDetails, // Chỉ chi phí trong tháng
                CreditorSummaries = creditorSummaries, // Nợ từ tất cả các tháng
                CurrentUserId = userId
            };
        }

        // GET: API/Reports/GetPendingPaymentsCount
        [HttpGet]
        public async Task<IActionResult> GetPendingPaymentsCount()
        {
            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;
            var currentUserId = SessionHelper.GetUserId(HttpContext);

            var query = _context.MonthlyPayments
                .Where(mp => mp.Status == "Pending" && mp.Year == currentYear && mp.Month == currentMonth)
                .AsQueryable();

            // Filter by permissions
            if (SessionHelper.IsSuperAdmin(HttpContext))
            {
                // SuperAdmin thấy tất cả
            }
            else if (SessionHelper.IsAdmin(HttpContext))
            {
                // Admin chỉ thấy payments trong nhóm của mình
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    query = query.Where(mp => mp.GroupId == groupId.Value);
                }
                else
                {
                    query = query.Where(mp => false);
                }
            }
            else if (SessionHelper.IsUser(HttpContext) && currentUserId.HasValue)
            {
                // User thường chỉ thấy payments mà họ là creditor (người được nợ)
                // Lọc theo CreditorId: nếu payment có CreditorId thì dùng CreditorId, nếu không thì fallback về expense
                var startDate = new DateTime(currentYear, currentMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Lấy tất cả payments trong tháng đó
                var allPayments = await query.ToListAsync();

                // Lọc payments: chỉ lấy những payment mà user là creditor
                var validPaymentIds = new List<int>();
                foreach (var payment in allPayments)
                {
                    bool isForCurrentUser = false;

                    // Nếu payment có CreditorId, kiểm tra xem có phải user hiện tại không
                    if (payment.CreditorId.HasValue)
                    {
                        isForCurrentUser = payment.CreditorId.Value == currentUserId.Value;
                    }
                    else
                    {
                        // Nếu không có CreditorId, fallback về kiểm tra expense (cho các payment cũ)
                        var isCreditor = await _context.Expenses
                            .Include(e => e.Participants)
                            .AnyAsync(e => e.PayerId == currentUserId.Value &&
                                          e.Participants.Any(p => p.UserId == payment.UserId && p.UserId != currentUserId.Value) &&
                                          e.ExpenseDate >= startDate && e.ExpenseDate <= endDate);

                        if (isCreditor)
                        {
                            isForCurrentUser = true;
                        }
                    }

                    if (isForCurrentUser)
                    {
                        validPaymentIds.Add(payment.Id);
                    }
                }

                if (validPaymentIds.Any())
                {
                    query = query.Where(mp => validPaymentIds.Contains(mp.Id));
                }
                else
                {
                    query = query.Where(mp => false);
                }
            }
            else
            {
                query = query.Where(mp => false);
            }

            var count = await query.CountAsync();

            return Json(new { count = count });
        }
    }
}
