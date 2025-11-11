using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models.ViewModels;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SettingsController(
            IConfiguration configuration,
            IWebHostEnvironment webHostEnvironment)
        {
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            // Chỉ SuperAdmin mới được truy cập
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new SettingsViewModel
            {
                // ConnectionStrings
                DefaultConnection = _configuration["ConnectionStrings:DefaultConnection"] ?? "",

                // Casso
                CassoWebhookSecret = _configuration["Casso:WebhookSecret"] ?? "",
                CassoSecureToken = _configuration["Casso:SecureToken"] ?? "",
                CassoWebhookVersion = _configuration["Casso:WebhookVersion"] ?? "",

                // Payment
                PaymentDescriptionPrefix = _configuration["Payment:DescriptionPrefix"] ?? "",
                PaymentDescriptionSuffix = _configuration["Payment:DescriptionSuffix"] ?? "",
                PaymentDescriptionSeparator = _configuration["Payment:DescriptionSeparator"] ?? "",

                // Telegram
                TelegramBotToken = _configuration["Telegram:BotToken"],
                TelegramBotUsername = _configuration["Telegram:BotUsername"] ?? "",

                // Parking Payment
                ParkingDefaultLicensePlate = _configuration["ParkingPayment:DefaultLicensePlate"] ?? "",
                ParkingMotorbikePrice = decimal.TryParse(_configuration["ParkingPayment:Prices:Motorbike"], out var motorbikePrice) ? motorbikePrice : 0,
                ParkingCarPrice = decimal.TryParse(_configuration["ParkingPayment:Prices:Car"], out var carPrice) ? carPrice : 0,
                ParkingCarOvernightPrice = decimal.TryParse(_configuration["ParkingPayment:Prices:CarOvernight"], out var carOvernightPrice) ? carOvernightPrice : 0,
                ParkingCompanyBankName = _configuration["ParkingPayment:CompanyAccount:BankName"] ?? "",
                ParkingCompanyAccountNumber = _configuration["ParkingPayment:CompanyAccount:AccountNumber"] ?? "",
                ParkingCompanyAccountHolder = _configuration["ParkingPayment:CompanyAccount:AccountHolder"] ?? "",
                ParkingStaffBankName = _configuration["ParkingPayment:StaffAccount:BankName"] ?? "",
                ParkingStaffAccountNumber = _configuration["ParkingPayment:StaffAccount:AccountNumber"] ?? "",
                ParkingStaffAccountHolder = _configuration["ParkingPayment:StaffAccount:AccountHolder"] ?? "",

                // Avatar
                AvatarUploadPath = _configuration["Avatar:UploadPath"] ?? "",
                AvatarMaxFileSize = int.TryParse(_configuration["Avatar:MaxFileSize"], out var maxFileSize) ? maxFileSize : 0,
                AvatarAllowedExtensions = _configuration["Avatar:AllowedExtensions"] ?? ""
            };

            // Lấy thông tin Telegram webhook
            var (success, webhookUrl, error) = await TelegramHelper.GetWebhookInfoAsync();
            if (success)
            {
                viewModel.TelegramWebhookEnabled = !string.IsNullOrEmpty(webhookUrl);
                viewModel.TelegramWebhookUrl = webhookUrl;
            }
            else
            {
                viewModel.TelegramWebhookError = error;
            }

            return View(viewModel);
        }

        // POST: Settings/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(SettingsViewModel viewModel)
        {
            // Chỉ SuperAdmin mới được truy cập
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này.";
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                return View("Index", viewModel);
            }

            try
            {
                var appsettingsPath = Path.Combine(_webHostEnvironment.ContentRootPath, "appsettings.json");

                // Đọc file appsettings.json
                var jsonContent = await System.IO.File.ReadAllTextAsync(appsettingsPath);
                var jsonDoc = JsonNode.Parse(jsonContent);

                if (jsonDoc == null)
                {
                    TempData["ErrorMessage"] = "Không thể đọc file appsettings.json.";
                    return RedirectToAction(nameof(Index));
                }

                // Cập nhật các giá trị
                // ConnectionStrings
                jsonDoc["ConnectionStrings"]!["DefaultConnection"] = viewModel.DefaultConnection;

                // Casso
                jsonDoc["Casso"]!["WebhookSecret"] = viewModel.CassoWebhookSecret;
                jsonDoc["Casso"]!["SecureToken"] = viewModel.CassoSecureToken;
                jsonDoc["Casso"]!["WebhookVersion"] = viewModel.CassoWebhookVersion;

                // Payment
                jsonDoc["Payment"]!["DescriptionPrefix"] = viewModel.PaymentDescriptionPrefix;
                jsonDoc["Payment"]!["DescriptionSuffix"] = viewModel.PaymentDescriptionSuffix;
                jsonDoc["Payment"]!["DescriptionSeparator"] = viewModel.PaymentDescriptionSeparator;

                // Telegram - chỉ cập nhật nếu có giá trị mới
                if (!string.IsNullOrWhiteSpace(viewModel.TelegramBotToken))
                {
                    jsonDoc["Telegram"]!["BotToken"] = viewModel.TelegramBotToken;
                }
                jsonDoc["Telegram"]!["BotUsername"] = viewModel.TelegramBotUsername;

                // Parking Payment
                jsonDoc["ParkingPayment"]!["DefaultLicensePlate"] = viewModel.ParkingDefaultLicensePlate;
                jsonDoc["ParkingPayment"]!["Prices"]!["Motorbike"] = viewModel.ParkingMotorbikePrice;
                jsonDoc["ParkingPayment"]!["Prices"]!["Car"] = viewModel.ParkingCarPrice;
                jsonDoc["ParkingPayment"]!["Prices"]!["CarOvernight"] = viewModel.ParkingCarOvernightPrice;
                jsonDoc["ParkingPayment"]!["CompanyAccount"]!["BankName"] = viewModel.ParkingCompanyBankName;
                jsonDoc["ParkingPayment"]!["CompanyAccount"]!["AccountNumber"] = viewModel.ParkingCompanyAccountNumber;
                jsonDoc["ParkingPayment"]!["CompanyAccount"]!["AccountHolder"] = viewModel.ParkingCompanyAccountHolder;
                jsonDoc["ParkingPayment"]!["StaffAccount"]!["BankName"] = viewModel.ParkingStaffBankName;
                jsonDoc["ParkingPayment"]!["StaffAccount"]!["AccountNumber"] = viewModel.ParkingStaffAccountNumber;
                jsonDoc["ParkingPayment"]!["StaffAccount"]!["AccountHolder"] = viewModel.ParkingStaffAccountHolder;

                // Avatar
                jsonDoc["Avatar"]!["UploadPath"] = viewModel.AvatarUploadPath;
                jsonDoc["Avatar"]!["MaxFileSize"] = viewModel.AvatarMaxFileSize;
                jsonDoc["Avatar"]!["AllowedExtensions"] = viewModel.AvatarAllowedExtensions;

                // Ghi lại file với format đẹp
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var updatedJson = jsonDoc.ToJsonString(options);

                // Lưu giá trị cũ trước khi reload để kiểm tra
                var oldConnectionString = _configuration["ConnectionStrings:DefaultConnection"];
                var connectionStringChanged = oldConnectionString != viewModel.DefaultConnection;

                await System.IO.File.WriteAllTextAsync(appsettingsPath, updatedJson);

                // Reload configuration động
                try
                {
                    // Reload IConfigurationRoot - điều này sẽ reload tất cả configuration providers
                    // bao gồm cả appsettings.json
                    if (_configuration is IConfigurationRoot configRoot)
                    {
                        configRoot.Reload();
                        Log.Information("Đã reload IConfigurationRoot - tất cả settings đã được cập nhật");
                    }
                    else
                    {
                        Log.Warning("IConfiguration không phải là IConfigurationRoot, không thể reload");
                    }

                    // Reload các helper classes có static state
                    TelegramHelper.Reload(_configuration);
                    IdEncoderHelper.Reload(_configuration);

                    // ParkingPaymentController sử dụng computed properties (=>) đọc trực tiếp từ _configuration
                    // Sau khi IConfigurationRoot.Reload() được gọi, các properties này sẽ tự động lấy giá trị mới
                    // khi được truy cập lần sau (không cần reload riêng)
                    Log.Information("Đã cập nhật và reload appsettings.json bởi SuperAdmin");
                    Log.Information("ParkingPayment, Casso, Avatar và các settings khác sẽ tự động áp dụng ngay lập tức");

                    // Kiểm tra xem có settings nào cần restart không
                    var needsRestart = false;
                    var restartReasons = new List<string>();

                    // ConnectionStrings thay đổi cần restart vì DbContext đã được khởi tạo
                    if (connectionStringChanged)
                    {
                        needsRestart = true;
                        restartReasons.Add("Connection String");
                    }

                    var message = "Đã cập nhật cấu hình thành công và áp dụng ngay lập tức!";
                    if (needsRestart)
                    {
                        message += $" Lưu ý: Các thay đổi sau cần restart ứng dụng: {string.Join(", ", restartReasons)}";
                    }

                    TempData["SuccessMessage"] = message;
                }
                catch (Exception reloadEx)
                {
                    Log.Warning(reloadEx, "Không thể reload configuration động, một số thay đổi có thể cần restart");
                    TempData["SuccessMessage"] = "Đã cập nhật cấu hình thành công! Một số thay đổi có thể cần restart ứng dụng để có hiệu lực.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi khi cập nhật appsettings.json");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật cấu hình: " + ex.Message;
                return View("Index", viewModel);
            }
        }

        // POST: Settings/ToggleTelegramWebhook
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTelegramWebhook(bool enable)
        {
            // Chỉ SuperAdmin mới được truy cập
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            try
            {
                if (enable)
                {
                    // Set webhook
                    var request = HttpContext.Request;
                    var baseUrl = $"{request.Scheme}://{request.Host}";
                    var webhookUrl = $"{baseUrl}/TelegramWebhook/Update";

                    var (success, errorMessage) = await TelegramHelper.SetWebhookAsync(webhookUrl);
                    if (success)
                    {
                        Log.Information("SuperAdmin đã bật Telegram webhook: {WebhookUrl}", webhookUrl);
                        return Json(new { success = true, message = "Đã bật Telegram webhook thành công!", webhookUrl = webhookUrl });
                    }
                    else
                    {
                        return Json(new { success = false, message = $"Không thể bật webhook: {errorMessage}" });
                    }
                }
                else
                {
                    // Delete webhook
                    var (success, errorMessage) = await TelegramHelper.DeleteWebhookAsync();
                    if (success)
                    {
                        Log.Information("SuperAdmin đã tắt Telegram webhook");
                        return Json(new { success = true, message = "Đã tắt Telegram webhook thành công!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = $"Không thể tắt webhook: {errorMessage}" });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi khi toggle Telegram webhook");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
    }
}

