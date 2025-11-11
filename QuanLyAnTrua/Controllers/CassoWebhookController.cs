using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using System.Text;

namespace QuanLyAnTrua.Controllers
{
    [ApiController]
    [Route("api/casso")]
    public class CassoWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CassoWebhookController> _logger;

        public CassoWebhookController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<CassoWebhookController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: Test parse description (for debugging)
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpGet("test-parse")]
        public IActionResult TestParseDescription(string description, string? api_key = null)
        {
            // Kiểm tra API key
            var expectedApiKey = _configuration["ApiKeys:TestParse"];
            if (string.IsNullOrEmpty(expectedApiKey))
            {
                _logger.LogWarning("TestParse API key is not configured");
                return StatusCode(500, new { error = "API key is not configured" });
            }

            if (string.IsNullOrEmpty(api_key) || !string.Equals(api_key, expectedApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "Invalid or missing API key" });
            }

            if (string.IsNullOrEmpty(description))
            {
                return BadRequest(new { error = "Description is required" });
            }

            var paymentInfo = IdEncoderHelper.ParsePaymentDescription(description);

            if (paymentInfo.HasValue)
            {
                // Kiểm tra xem user và creditor có tồn tại không
                var user = _context.Users.FirstOrDefault(u => u.Id == paymentInfo.Value.userId && u.IsActive);
                var creditor = _context.Users.FirstOrDefault(u => u.Id == paymentInfo.Value.creditorId && u.IsActive);

                return Ok(new
                {
                    success = true,
                    description = description,
                    creditorId = paymentInfo.Value.creditorId,
                    userId = paymentInfo.Value.userId,
                    year = paymentInfo.Value.year,
                    month = paymentInfo.Value.month,
                    userExists = user != null,
                    userName = user?.Name,
                    creditorExists = creditor != null,
                    creditorName = creditor?.Name
                });
            }
            else
            {
                // Thử decode từng phần để debug
                var parts = description.Split('-');
                object? debugInfo = null;

                if (parts.Length >= 2)
                {
                    var decodedCreditorId = IdEncoderHelper.DecodeCreditorId(parts[1]);
                    debugInfo = new
                    {
                        parts = parts,
                        partCount = parts.Length,
                        firstPart = parts.Length > 0 ? parts[0] : null,
                        secondPart = parts.Length > 1 ? parts[1] : null,
                        thirdPart = parts.Length > 2 ? parts[2] : null,
                        decodedCreditorId = decodedCreditorId
                    };
                }
                else
                {
                    debugInfo = new
                    {
                        parts = parts,
                        partCount = parts.Length
                    };
                }

                return Ok(new
                {
                    success = false,
                    description = description,
                    error = "Failed to parse description",
                    debug = debugInfo
                });
            }
        }

        [HttpPost("webhook")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> Webhook([FromQuery] string? token = null)
        {
            string? rawBody = null;
            try
            {
                // Đọc raw body trước khi model binding
                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                rawBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("Received webhook request. Body length: {Length}, Token: {Token}",
                    rawBody?.Length ?? 0, token ?? "null");

                // Deserialize request
                CassoWebhookRequest? request = null;
                try
                {
                    if (string.IsNullOrEmpty(rawBody))
                    {
                        _logger.LogWarning("Empty request body");
                        return BadRequest(new { error = "Request body is required" });
                    }

                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    request = System.Text.Json.JsonSerializer.Deserialize<CassoWebhookRequest>(rawBody, options);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize webhook request. Raw body: {RawBody}", rawBody);
                    return BadRequest(new { error = "Invalid request body", details = ex.Message });
                }

                if (request == null)
                {
                    _logger.LogWarning("Deserialized request is null. Raw body: {RawBody}", rawBody);
                    return BadRequest(new { error = "Invalid request body" });
                }

                _logger.LogInformation("Deserialized request: Error={Error}, Data={Data}, Transactions={Transactions}",
                    request.Error,
                    request.Data != null ? $"Id={request.Data.Id}, Description={request.Data.Description}, Amount={request.Data.Amount}" : "null",
                    request.Transactions != null ? $"Count={request.Transactions.Count}" : "null");

                // Log chi tiết description nếu có để debug
                if (request.Data != null && !string.IsNullOrEmpty(request.Data.Description))
                {
                    _logger.LogInformation("Transaction Description from webhook: '{Description}'", request.Data.Description);
                    var testParse = IdEncoderHelper.ParsePaymentDescription(request.Data.Description);
                    if (testParse.HasValue)
                    {
                        _logger.LogInformation("Description parsed successfully: CreditorId={CreditorId}, UserId={UserId}, Year={Year}, Month={Month}",
                            testParse.Value.creditorId, testParse.Value.userId, testParse.Value.year, testParse.Value.month);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse description: '{Description}'. Will try to decode manually.", request.Data.Description);
                        // Thử decode thủ công để debug
                        var parts = request.Data.Description.Split('-');
                        if (parts.Length >= 2)
                        {
                            var decodedCreditorId = IdEncoderHelper.DecodeCreditorId(parts[1]);
                            _logger.LogWarning("Manual decode attempt - Parts: {Parts}, DecodedCreditorId: {DecodedCreditorId}",
                                string.Join(", ", parts), decodedCreditorId);
                        }
                    }
                }

                // Xác thực request - Hỗ trợ nhiều WebhookSecret theo từng tài khoản ngân hàng
                var webhookVersion = _configuration["Casso:WebhookVersion"] ?? "V2";
                var isValid = false;
                string? webhookSecret = null;

                try
                {
                    // Lấy AccountNumber từ transaction để tìm user và WebhookSecret tương ứng
                    string? accountNumber = null;
                    if (request.Data != null && !string.IsNullOrEmpty(request.Data.AccountNumber))
                    {
                        accountNumber = request.Data.AccountNumber;
                    }
                    else if (request.Transactions != null && request.Transactions.Any())
                    {
                        accountNumber = request.Transactions.FirstOrDefault()?.AccountNumber;
                    }

                    // Tìm user theo AccountNumber để lấy WebhookSecret riêng
                    User? accountUser = null;
                    if (!string.IsNullOrEmpty(accountNumber))
                    {
                        accountUser = await _context.Users
                            .FirstOrDefaultAsync(u => u.BankAccount == accountNumber && u.IsActive);

                        if (accountUser != null && !string.IsNullOrEmpty(accountUser.CassoWebhookSecret))
                        {
                            webhookSecret = accountUser.CassoWebhookSecret;
                            _logger.LogInformation("Found user-specific WebhookSecret for account {AccountNumber}, UserId={UserId}, UserName={UserName}",
                                accountNumber, accountUser.Id, accountUser.Name);
                        }
                        else if (accountUser != null)
                        {
                            _logger.LogInformation("User found for account {AccountNumber} but no WebhookSecret configured, will use default from appsettings",
                                accountNumber);
                        }
                    }

                    // Fallback về WebhookSecret trong appsettings.json nếu user không có
                    if (string.IsNullOrEmpty(webhookSecret))
                    {
                        webhookSecret = _configuration["Casso:WebhookSecret"];
                        _logger.LogInformation("Using default WebhookSecret from appsettings.json");
                    }

                    if (webhookVersion == "V2")
                    {
                        // Webhook V2: Xác thực bằng chữ ký số
                        var signature = Request.Headers["X-Casso-Signature"].FirstOrDefault();

                        _logger.LogInformation("Webhook V2 - Signature: {Signature}, Secret configured: {HasSecret}, AccountNumber: {AccountNumber}",
                            signature != null ? "Present" : "Missing", !string.IsNullOrEmpty(webhookSecret), accountNumber ?? "null");

                        // Nếu không có signature, có thể là request test từ Casso
                        if (string.IsNullOrEmpty(signature))
                        {
                            _logger.LogInformation("No signature header - treating as test request");
                            // Cho phép request test đi qua (Casso sẽ test endpoint trước)
                            isValid = true;
                        }
                        else if (string.IsNullOrEmpty(webhookSecret))
                        {
                            _logger.LogWarning("Missing webhook secret for account {AccountNumber}", accountNumber ?? "unknown");
                            return Unauthorized(new { error = "Unauthorized" });
                        }
                        else
                        {
                            // Parse JSON từ raw body để xác thực
                            object? jsonData = null;
                            try
                            {
                                jsonData = System.Text.Json.JsonSerializer.Deserialize<object>(rawBody);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to deserialize raw body for signature verification");
                                return BadRequest(new { error = "Invalid JSON format" });
                            }

                            if (jsonData == null)
                            {
                                _logger.LogWarning("Deserialized JSON data is null for signature verification");
                                return BadRequest(new { error = "Invalid JSON format" });
                            }

                            isValid = CassoWebhookHelper.VerifyWebhookSignature(
                                signature,
                                jsonData,
                                webhookSecret
                            );

                            _logger.LogInformation("Signature verification result: {IsValid} for account {AccountNumber}",
                                isValid, accountNumber ?? "unknown");
                        }
                    }
                    else
                    {
                        // Webhook cũ: Xác thực bằng secure-token
                        var secureToken = Request.Headers["secure-token"].FirstOrDefault();
                        var expectedToken = webhookSecret; // Dùng WebhookSecret làm SecureToken cho V1

                        _logger.LogInformation("Webhook V1 - Token: {Token}, Expected token configured: {HasToken}, AccountNumber: {AccountNumber}",
                            secureToken != null ? "Present" : "Missing", !string.IsNullOrEmpty(expectedToken), accountNumber ?? "null");

                        // Nếu không có secure-token, có thể là request test
                        if (string.IsNullOrEmpty(secureToken))
                        {
                            _logger.LogInformation("No secure-token header - treating as test request");
                            isValid = true;
                        }
                        else if (string.IsNullOrEmpty(expectedToken))
                        {
                            _logger.LogWarning("Missing expected token for account {AccountNumber}", accountNumber ?? "unknown");
                            return Unauthorized(new { error = "Unauthorized" });
                        }
                        else
                        {
                            isValid = CassoWebhookHelper.VerifySecureToken(secureToken, expectedToken);
                            _logger.LogInformation("Token verification result: {IsValid} for account {AccountNumber}",
                                isValid, accountNumber ?? "unknown");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during webhook authentication");
                    return StatusCode(500, new { error = "Authentication error", details = ex.Message });
                }

                if (!isValid)
                {
                    _logger.LogWarning("Webhook signature verification failed");
                    return Unauthorized(new { error = "Unauthorized" });
                }

                // Kiểm tra error code
                if (request.Error != 0)
                {
                    _logger.LogWarning("Webhook request has error: {Error}", request.Error);
                    return Ok(new { message = "Error in webhook data" });
                }

                // Xử lý giao dịch
                var transactions = new List<CassoTransaction>();

                // Webhook V2: Data là object đơn
                if (request.Data != null)
                {
                    transactions.Add(request.Data);
                }
                // Webhook cũ: Transactions là array
                else if (request.Transactions != null && request.Transactions.Any())
                {
                    transactions.AddRange(request.Transactions);
                }

                if (!transactions.Any())
                {
                    _logger.LogInformation("No transactions in webhook request");
                    return Ok(new { message = "No transactions" });
                }

                var processedCount = 0;
                var errors = new List<string>();

                foreach (var transaction in transactions)
                {
                    try
                    {
                        _logger.LogInformation("Processing transaction: Id={Id}, Description={Description}, Amount={Amount}",
                            transaction.Id, transaction.Description, transaction.Amount);

                        var result = await ProcessTransaction(transaction);
                        if (result.Success)
                        {
                            processedCount++;
                            _logger.LogInformation("Transaction processed successfully: Id={Id}", transaction.Id);
                        }
                        else
                        {
                            var errorMsg = result.ErrorMessage ?? "Unknown error";
                            _logger.LogWarning("Transaction processing failed: Id={Id}, Error={Error}",
                                transaction.Id, errorMsg);
                            errors.Add(errorMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing transaction {TransactionId}", transaction.Id);
                        errors.Add($"Transaction {transaction.Id}: {ex.Message}");
                    }
                }

                // Nếu có token, redirect đến PublicView
                if (!string.IsNullOrEmpty(token) && processedCount > 0)
                {
                    return RedirectToAction("PublicView", "Reports", new { token });
                }

                return Ok(new
                {
                    message = "Webhook processed",
                    processed = processedCount,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook. Raw body: {RawBody}", rawBody ?? "null");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        private async Task<(bool Success, string? ErrorMessage)> ProcessTransaction(CassoTransaction transaction)
        {
            try
            {
                _logger.LogInformation("Processing transaction: Id={Id}, Description={Description}, Amount={Amount}, AccountNumber={AccountNumber}",
                    transaction.Id, transaction.Description, transaction.Amount, transaction.AccountNumber);

                // Parse description để lấy creditorId và userId
                var paymentInfo = IdEncoderHelper.ParsePaymentDescription(transaction.Description);

                if (paymentInfo.HasValue)
                {
                    _logger.LogInformation("Parsed payment description: CreditorId={CreditorId}, UserId={UserId}, Year={Year}, Month={Month}",
                        paymentInfo.Value.creditorId, paymentInfo.Value.userId, paymentInfo.Value.year, paymentInfo.Value.month);
                }
                else
                {
                    _logger.LogWarning("Failed to parse payment description: {Description}", transaction.Description);
                }

                User? user = null;
                User? creditor = null;
                int? creditorId = null;

                if (paymentInfo.HasValue)
                {
                    // paymentInfo.Value.userId = Người thanh toán (người gửi tiền)
                    // paymentInfo.Value.creditorId = Người được thanh toán (người nhận tiền)

                    _logger.LogInformation("DEBUG: Parsed values - paymentInfo.Value.creditorId={CreditorId}, paymentInfo.Value.userId={UserId}, paymentInfo.Value.year={Year}, paymentInfo.Value.month={Month}",
                        paymentInfo.Value.creditorId, paymentInfo.Value.userId, paymentInfo.Value.year, paymentInfo.Value.month);

                    // Set creditorId ngay từ đầu để đảm bảo nó luôn được set
                    creditorId = paymentInfo.Value.creditorId;

                    _logger.LogInformation("DEBUG: creditorId variable set to {CreditorId}", creditorId);

                    // Tìm User theo userId từ description - Đây là người thanh toán
                    user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == paymentInfo.Value.userId && u.IsActive);

                    if (user == null)
                    {
                        _logger.LogWarning("User (người thanh toán) not found: UserId={UserId}", paymentInfo.Value.userId);
                        return (false, $"User {paymentInfo.Value.userId} not found");
                    }

                    _logger.LogInformation("Found user (người thanh toán): UserId={UserId}, UserName={UserName}", user.Id, user.Name);
                    _logger.LogInformation("DEBUG: After finding user - creditorId={CreditorId}, user.Id={UserId}", creditorId, user.Id);

                    // Tìm Creditor theo creditorId từ description - Đây là người được thanh toán
                    // Lưu ý: Không kiểm tra IsActive cho creditor vì có thể creditor đã inactive nhưng vẫn cần lưu creditorId
                    creditor = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == paymentInfo.Value.creditorId);

                    if (creditor == null)
                    {
                        _logger.LogWarning("Creditor (người được thanh toán) not found: CreditorId={CreditorId}, nhưng vẫn tiếp tục lưu creditorId", paymentInfo.Value.creditorId);
                        // Không return ở đây, vẫn tiếp tục với creditorId đã được set
                    }
                    else
                    {
                        _logger.LogInformation("Found creditor (người được thanh toán): CreditorId={CreditorId}, CreditorName={CreditorName}",
                            creditor.Id, creditor.Name);
                    }

                    _logger.LogInformation("DEBUG: Before creating payment - creditorId={CreditorId}, user.Id={UserId}", creditorId, user.Id);
                }
                else
                {
                    // Fallback: Tìm User theo accountNumber (logic cũ)
                    if (string.IsNullOrEmpty(transaction.AccountNumber))
                    {
                        return (false, "Cannot find user: missing accountNumber and invalid description format");
                    }

                    user = await _context.Users
                        .FirstOrDefaultAsync(u => u.BankAccount == transaction.AccountNumber && u.IsActive);

                    if (user == null)
                    {
                        _logger.LogWarning("User not found for account number: {AccountNumber}", transaction.AccountNumber);
                        return (false, $"User not found for account {transaction.AccountNumber}");
                    }
                }

                // Xác định Year/Month cho payment
                // Ưu tiên: Nếu có paymentInfo (parse từ description), dùng Year/Month từ description
                // Nếu không, dùng Year/Month từ transactionDate (ngày giao dịch từ ngân hàng)
                int year;
                int month;
                DateTime transactionDate;

                // Parse transactionDateTime để lấy ngày thanh toán (PaidDate)
                if (!string.IsNullOrEmpty(transaction.TransactionDateTime))
                {
                    if (DateTime.TryParse(transaction.TransactionDateTime, out var parsedDate))
                    {
                        transactionDate = parsedDate;
                    }
                    else
                    {
                        transactionDate = DateTime.Now;
                        _logger.LogWarning("Failed to parse transactionDateTime: {DateTime}, using current date", transaction.TransactionDateTime);
                    }
                }
                else if (!string.IsNullOrEmpty(transaction.When))
                {
                    // Webhook cũ sử dụng field "when"
                    if (DateTime.TryParse(transaction.When, out var parsedDate))
                    {
                        transactionDate = parsedDate;
                    }
                    else
                    {
                        transactionDate = DateTime.Now;
                        _logger.LogWarning("Failed to parse when: {DateTime}, using current date", transaction.When);
                    }
                }
                else
                {
                    transactionDate = DateTime.Now;
                }

                if (paymentInfo.HasValue)
                {
                    // Có paymentInfo: dùng Year/Month từ description (tháng/năm cần thanh toán)
                    year = paymentInfo.Value.year;
                    month = paymentInfo.Value.month;
                    _logger.LogInformation("Using Year/Month from description: Year={Year}, Month={Month}, PaidDate={PaidDate}",
                        year, month, transactionDate);
                }
                else
                {
                    // Không có paymentInfo: dùng Year/Month từ transactionDate (ngày giao dịch)
                    year = transactionDate.Year;
                    month = transactionDate.Month;
                    _logger.LogInformation("Using Year/Month from transactionDate: Year={Year}, Month={Month}, PaidDate={PaidDate}",
                        year, month, transactionDate);
                }

                // Làm tròn số tiền lên (round up) để khớp với số tiền trong QR code
                var roundedAmount = Math.Ceiling(transaction.Amount);

                // Kiểm tra trùng lặp (so sánh với số tiền đã làm tròn)
                var isDuplicate = await _context.MonthlyPayments
                    .AnyAsync(mp => mp.UserId == user.Id &&
                                   mp.Year == year &&
                                   mp.Month == month &&
                                   mp.PaidAmount == roundedAmount &&
                                   mp.PaidDate.Date == transactionDate.Date &&
                                   (!string.IsNullOrEmpty(transaction.Reference) ? mp.Notes != null && mp.Notes.Contains(transaction.Reference) : true) &&
                                   (creditorId.HasValue ? mp.CreditorId == creditorId.Value : true));

                if (isDuplicate)
                {
                    _logger.LogInformation("Duplicate transaction detected: UserId={UserId}, Amount={Amount}, Date={Date}",
                        user.Id, transaction.Amount, transactionDate);
                    return (false, "Duplicate transaction");
                }

                // Tạo MonthlyPayment
                // Nếu chưa có creditor (fallback case), tìm lại
                if (creditor == null && creditorId.HasValue)
                {
                    creditor = await _context.Users.FindAsync(creditorId.Value);
                }

                // Log thông tin trước khi tạo payment
                _logger.LogInformation("Creating payment: UserId={UserId} (người thanh toán: {UserName}), CreditorId={CreditorId} (người được thanh toán: {CreditorName}), Description={Description}",
                    user.Id, user.Name, creditorId, creditor?.Name ?? "null", transaction.Description);

                var notes = BuildNotes(transaction, user, creditor);

                // UserId = Người thanh toán (người gửi tiền)
                // CreditorId = Người được thanh toán (người nhận tiền)

                _logger.LogInformation("DEBUG: Before creating MonthlyPayment object - creditorId={CreditorId}, user.Id={UserId}",
                    creditorId, user.Id);

                var monthlyPayment = new MonthlyPayment
                {
                    UserId = user.Id, // Người thanh toán
                    CreditorId = creditorId, // Người được thanh toán (có thể null nếu không parse được description)
                    Year = year,
                    Month = month,
                    PaidAmount = roundedAmount, // Sử dụng số tiền đã làm tròn lên
                    PaidDate = transactionDate,
                    Notes = notes,
                    GroupId = user.GroupId,
                    Status = "Confirmed" // Payment từ webhook luôn được xác nhận tự động
                };

                _logger.LogInformation("DEBUG: After creating MonthlyPayment object - monthlyPayment.UserId={UserId}, monthlyPayment.CreditorId={CreditorId}",
                    monthlyPayment.UserId, monthlyPayment.CreditorId);

                // Log cảnh báo nếu CreditorId null
                if (!creditorId.HasValue)
                {
                    _logger.LogWarning("Payment created without CreditorId. Description={Description}, UserId={UserId}",
                        transaction.Description, user.Id);
                }

                _context.Add(monthlyPayment);

                _logger.LogInformation("DEBUG: Before SaveChanges - monthlyPayment.UserId={UserId}, monthlyPayment.CreditorId={CreditorId}",
                    monthlyPayment.UserId, monthlyPayment.CreditorId);

                await _context.SaveChangesAsync();

                _logger.LogInformation("DEBUG: After SaveChanges - monthlyPayment.Id={Id}, monthlyPayment.UserId={UserId}, monthlyPayment.CreditorId={CreditorId}",
                    monthlyPayment.Id, monthlyPayment.UserId, monthlyPayment.CreditorId);

                _logger.LogInformation("Created payment: Id={PaymentId}, UserId={UserId} (người thanh toán: {UserName}), CreditorId={CreditorId} (người được thanh toán: {CreditorName}), Amount={Amount}, Status={Status}",
                    monthlyPayment.Id, user.Id, user.Name, monthlyPayment.CreditorId, creditor?.Name ?? "null", transaction.Amount, monthlyPayment.Status);

                // Gửi thông báo Telegram cho người nhận tiền (creditor)
                if (creditor != null && !string.IsNullOrEmpty(creditor.TelegramUserId))
                {
                    try
                    {
                        var amountFormatted = roundedAmount.ToString("N0");
                        var message = $"💰 <b>Nhận được thanh toán</b>\n\n" +
                                     $"👤 Người gửi: <b>{user.Name}</b>\n" +
                                     $"💵 Số tiền: <b>{amountFormatted} VNĐ</b>\n" +
                                     $"📅 Tháng: <b>{month}/{year}</b>\n" +
                                     $"🕐 Thời gian: <b>{transactionDate:dd/MM/yyyy HH:mm}</b>";

                        var telegramSent = await TelegramHelper.SendHtmlMessageAsync(creditor.TelegramUserId, message);
                        if (telegramSent)
                        {
                            _logger.LogInformation("Đã gửi thông báo Telegram cho creditor {CreditorId} ({CreditorName}) về thanh toán từ {UserId} ({UserName})",
                                creditor.Id, creditor.Name, user.Id, user.Name);
                        }
                        else
                        {
                            _logger.LogWarning("Không thể gửi thông báo Telegram cho creditor {CreditorId} ({CreditorName})",
                                creditor.Id, creditor.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi gửi thông báo Telegram cho creditor {CreditorId}", creditor.Id);
                        // Không throw exception, chỉ log lỗi vì payment đã được tạo thành công
                    }
                }
                else if (creditor != null && string.IsNullOrEmpty(creditor.TelegramUserId))
                {
                    _logger.LogInformation("Creditor {CreditorId} ({CreditorName}) chưa cấu hình TelegramUserId, không gửi thông báo",
                        creditor.Id, creditor.Name);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessTransaction");
                return (false, ex.Message);
            }
        }

        private string BuildNotes(CassoTransaction transaction, User user, User? creditor)
        {
            var notes = new List<string>();

            // Thêm note chính: "{người gửi} chuyển khoản cho {người nhận} qua QR"
            if (creditor != null)
            {
                notes.Add($"{user.Name} chuyển khoản cho {creditor.Name} qua QR");
            }
            else
            {
                notes.Add($"{user.Name} chuyển khoản qua QR");
            }

            // Thêm nội dung chuyển khoản (description) để đối soát
            if (!string.IsNullOrEmpty(transaction.Description))
            {
                // Chỉ thêm description nếu không phải format ThanToan-... (vì đã có thông tin trong note chính)
                if (!transaction.Description.StartsWith("ThanToan-"))
                {
                    notes.Add($"Nội dung CK: {transaction.Description}");
                }
            }

            if (!string.IsNullOrEmpty(transaction.Reference))
            {
                notes.Add($"Reference: {transaction.Reference}");
            }

            if (!string.IsNullOrEmpty(transaction.CounterAccountName))
            {
                notes.Add($"Từ: {transaction.CounterAccountName}");
            }

            if (!string.IsNullOrEmpty(transaction.CounterAccountNumber))
            {
                notes.Add($"Số TK: {transaction.CounterAccountNumber}");
            }

            return string.Join(" | ", notes);
        }
    }
}

