using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        /// Reload bot token từ configuration (dùng khi config thay đổi động)
        /// </summary>
        public static void Reload(IConfiguration configuration)
        {
            var newToken = configuration["Telegram:BotToken"];
            if (!string.IsNullOrEmpty(newToken))
            {
                _botToken = newToken;
                Log.Information("Đã reload Telegram BotToken từ configuration");
            }
            else
            {
                Log.Warning("Telegram BotToken trống sau khi reload");
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

        /// <summary>
        /// Set webhook cho Telegram Bot
        /// </summary>
        /// <param name="webhookUrl">URL của webhook endpoint</param>
        /// <returns>True nếu set thành công, False nếu có lỗi</returns>
        public static async Task<(bool Success, string? ErrorMessage)> SetWebhookAsync(string webhookUrl)
        {
            if (string.IsNullOrEmpty(_botToken))
            {
                return (false, "Telegram BotToken chưa được cấu hình");
            }

            if (string.IsNullOrEmpty(webhookUrl))
            {
                return (false, "Webhook URL không được để trống");
            }

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/setWebhook";
                var payload = new
                {
                    url = webhookUrl
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Đã set Telegram webhook thành công: {WebhookUrl}", webhookUrl);
                    return (true, null);
                }
                else
                {
                    Log.Warning("Lỗi khi set Telegram webhook, StatusCode: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    return (false, $"Lỗi: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception khi set Telegram webhook");
                return (false, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Xóa webhook cho Telegram Bot
        /// </summary>
        /// <returns>True nếu xóa thành công, False nếu có lỗi</returns>
        public static async Task<(bool Success, string? ErrorMessage)> DeleteWebhookAsync()
        {
            if (string.IsNullOrEmpty(_botToken))
            {
                return (false, "Telegram BotToken chưa được cấu hình");
            }

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/deleteWebhook";
                var response = await _httpClient.PostAsync(url, null);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Đã xóa Telegram webhook thành công");
                    return (true, null);
                }
                else
                {
                    Log.Warning("Lỗi khi xóa Telegram webhook, StatusCode: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    return (false, $"Lỗi: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception khi xóa Telegram webhook");
                return (false, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy thông tin webhook hiện tại
        /// </summary>
        /// <returns>Thông tin webhook hoặc null nếu có lỗi</returns>
        public static async Task<(bool Success, string? WebhookUrl, string? ErrorMessage)> GetWebhookInfoAsync()
        {
            if (string.IsNullOrEmpty(_botToken))
            {
                return (false, null, "Telegram BotToken chưa được cấu hình");
            }

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/getWebhookInfo";
                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(responseContent);
                    if (jsonDoc.RootElement.TryGetProperty("result", out var result))
                    {
                        if (result.TryGetProperty("url", out var urlElement))
                        {
                            var webhookUrl = urlElement.GetString();
                            return (true, webhookUrl, null);
                        }
                    }
                    return (true, null, null); // Webhook chưa được set
                }
                else
                {
                    Log.Warning("Lỗi khi lấy thông tin Telegram webhook, StatusCode: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    return (false, null, $"Lỗi: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception khi lấy thông tin Telegram webhook");
                return (false, null, $"Exception: {ex.Message}");
            }
        }
    }
}

