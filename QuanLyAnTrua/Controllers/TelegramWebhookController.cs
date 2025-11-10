using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using Serilog;
using System.Text;
using System.Text.Json;

namespace QuanLyAnTrua.Controllers
{
    [AllowAnonymous]
    public class TelegramWebhookController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public TelegramWebhookController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: TelegramWebhook/Update
        [HttpPost]
        public async Task<IActionResult> Update()
        {
            try
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();

                Log.Information("Received Telegram webhook update: {Body}", body);

                var update = JsonSerializer.Deserialize<JsonElement>(body);

                // Kiểm tra xem có message không
                if (!update.TryGetProperty("message", out var messageElement))
                {
                    return Ok(); // Không phải message, bỏ qua
                }

                // Lấy chat_id và text
                if (!messageElement.TryGetProperty("chat", out var chatElement) ||
                    !chatElement.TryGetProperty("id", out var chatIdElement) ||
                    !messageElement.TryGetProperty("text", out var textElement))
                {
                    return Ok();
                }

                var chatId = chatIdElement.GetInt64().ToString();
                var text = textElement.GetString();

                Log.Information("Telegram message - ChatId: {ChatId}, Text: {Text}", chatId, text);

                if (string.IsNullOrEmpty(text))
                {
                    return Ok();
                }

                // Xử lý lệnh /start
                if (text.StartsWith("/start"))
                {
                    await TelegramHelper.SendMessageAsync(chatId,
                        "👋 Chào mừng bạn đến với hệ thống Quản Lý Ăn Trưa!\n\n" +
                        "Để kết nối tài khoản Telegram với hệ thống, vui lòng nhắn tin theo cú pháp:\n\n" +
                        "📝 /set username|password\n\n" +
                        "Ví dụ: /set admin|123456\n\n" +
                        "Sau khi kết nối thành công, bạn sẽ nhận thông báo qua Telegram khi có chi phí mới.",
                        null);
                    return Ok();
                }

                // Xử lý lệnh /set username|password
                if (text.StartsWith("/set"))
                {
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        await TelegramHelper.SendMessageAsync(chatId,
                            "❌ Cú pháp không đúng!\n\n" +
                            "Vui lòng sử dụng: /set username|password\n\n" +
                            "Ví dụ: /set admin|123456",
                            null);
                        return Ok();
                    }

                    // Parse username|password
                    var credentials = parts[1].Split('|');
                    if (credentials.Length != 2)
                    {
                        await TelegramHelper.SendMessageAsync(chatId,
                            "❌ Cú pháp không đúng!\n\n" +
                            "Vui lòng sử dụng: /set username|password\n\n" +
                            "Ví dụ: /set admin|123456",
                            null);
                        return Ok();
                    }

                    var username = credentials[0].Trim();
                    var password = credentials[1].Trim();

                    // Tìm và xác thực user
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

                    if (user == null)
                    {
                        await TelegramHelper.SendMessageAsync(chatId,
                            "❌ Không tìm thấy tài khoản với username này.",
                            null);
                        Log.Warning("Không tìm thấy user với username: {Username}", username);
                        return Ok();
                    }

                    // Kiểm tra password
                    if (string.IsNullOrEmpty(user.PasswordHash) ||
                        !PasswordHelper.VerifyPassword(password, user.PasswordHash))
                    {
                        await TelegramHelper.SendMessageAsync(chatId,
                            "❌ Mật khẩu không đúng!",
                            null);
                        Log.Warning("Mật khẩu sai cho user: {Username}", username);
                        return Ok();
                    }

                    // Cập nhật TelegramUserId
                    user.TelegramUserId = chatId;
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    Log.Information("Đã cập nhật TelegramUserId cho user {UserId} ({Username}) qua /set: {ChatId}",
                        user.Id, user.Username, chatId);

                    await TelegramHelper.SendMessageAsync(chatId,
                        $"✅ Đã kết nối thành công!\n\n" +
                        $"Tài khoản: {user.Name}\n" +
                        $"Username: {user.Username}\n\n" +
                        $"Bạn sẽ nhận thông báo qua Telegram khi có chi phí mới.",
                        null);
                    return Ok();
                }

                // Không phải lệnh được hỗ trợ
                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi khi xử lý Telegram webhook");
                return Ok(); // Trả về OK để Telegram không retry
            }
        }
    }
}

