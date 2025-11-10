using System.Text;
using System.Text.Json;
using Serilog;

namespace QuanLyAnTrua.Helpers
{
    public static class TelegramHelper
    {
        private static string? _botToken;
        private static readonly HttpClient _httpClient = new HttpClient();

        public static void Initialize(IConfiguration configuration)
        {
            _botToken = configuration["Telegram:BotToken"];
            if (string.IsNullOrEmpty(_botToken))
            {
                Log.Warning("Telegram BotToken không được cấu hình trong appsettings.json");
            }
        }

        /// <summary>
        /// Gửi message qua Telegram Bot API
        /// </summary>
        /// <param name="chatId">Telegram User ID hoặc Chat ID</param>
        /// <param name="message">Nội dung message</param>
        /// <param name="parseMode">Parse mode (HTML, Markdown, MarkdownV2)</param>
        /// <returns>True nếu gửi thành công, False nếu có lỗi</returns>
        public static async Task<bool> SendMessageAsync(string chatId, string message, string? parseMode = "HTML")
        {
            if (string.IsNullOrEmpty(_botToken))
            {
                Log.Warning("Telegram BotToken chưa được cấu hình, không thể gửi message");
                return false;
            }

            if (string.IsNullOrEmpty(chatId))
            {
                Log.Warning("Telegram User ID trống, không thể gửi message");
                return false;
            }

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

                // Chỉ thêm parse_mode vào payload nếu nó không null
                object payload;
                if (!string.IsNullOrEmpty(parseMode))
                {
                    payload = new
                    {
                        chat_id = chatId,
                        text = message,
                        parse_mode = parseMode,
                        disable_web_page_preview = false
                    };
                }
                else
                {
                    payload = new
                    {
                        chat_id = chatId,
                        text = message,
                        disable_web_page_preview = false
                    };
                }

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Đã gửi Telegram message thành công đến chatId: {ChatId}", chatId);
                    return true;
                }
                else
                {
                    // Log chi tiết lỗi để debug
                    Log.Warning("Lỗi khi gửi Telegram message đến chatId: {ChatId}, StatusCode: {StatusCode}, Response: {Response}",
                        chatId, response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception khi gửi Telegram message đến chatId: {ChatId}", chatId);
                return false;
            }
        }

        /// <summary>
        /// Gửi message với HTML formatting
        /// </summary>
        public static async Task<bool> SendHtmlMessageAsync(string chatId, string message)
        {
            return await SendMessageAsync(chatId, message, "HTML");
        }

        /// <summary>
        /// Gửi message với Markdown formatting (Markdown cũ)
        /// </summary>
        public static async Task<bool> SendMarkdownMessageAsync(string chatId, string message)
        {
            return await SendMessageAsync(chatId, message, "Markdown");
        }

        /// <summary>
        /// Gửi message với MarkdownV2 formatting (Markdown mới - khuyến nghị)
        /// </summary>
        public static async Task<bool> SendMarkdownV2MessageAsync(string chatId, string message)
        {
            return await SendMessageAsync(chatId, message, "MarkdownV2");
        }
    }
}

