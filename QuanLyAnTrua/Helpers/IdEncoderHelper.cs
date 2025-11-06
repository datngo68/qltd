using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace QuanLyAnTrua.Helpers
{
    public static class IdEncoderHelper
    {
        private static string _prefix = "ThanToan"; // Default value
        private static string? _suffix = null; // Optional suffix

        /// <summary>
        /// Khởi tạo prefix và suffix từ configuration
        /// Nên gọi method này trong Program.cs khi app start
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            var prefix = configuration["Payment:DescriptionPrefix"];
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                _prefix = prefix.Trim();
            }

            var suffix = configuration["Payment:DescriptionSuffix"];
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                _suffix = suffix.Trim();
            }
        }

        /// <summary>
        /// Lấy prefix hiện tại
        /// </summary>
        public static string GetPrefix() => _prefix;

        /// <summary>
        /// Lấy suffix hiện tại (có thể null)
        /// </summary>
        public static string? GetSuffix() => _suffix;

        /// <summary>
        /// Mã hóa ID người được thanh toán thành chuỗi ngắn (Base64Url)
        /// </summary>
        public static string EncodeCreditorId(int creditorId)
        {
            var bytes = BitConverter.GetBytes(creditorId);
            var base64 = Convert.ToBase64String(bytes);
            // Chuyển thành Base64Url (URL-safe)
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Giải mã chuỗi để lấy lại creditorId
        /// </summary>
        public static int? DecodeCreditorId(string encoded)
        {
            try
            {
                // Chuyển từ Base64Url về Base64
                var base64 = encoded.Replace('-', '+').Replace('_', '/');
                // Thêm padding nếu cần
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }
                var bytes = Convert.FromBase64String(base64);
                return BitConverter.ToInt32(bytes, 0);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tạo description cho thanh toán với format: {Prefix}-{encodedCreditorId}-{userId}[-{Suffix}]
        /// Nếu có suffix, sẽ thêm vào cuối để dễ dàng parse chính xác
        /// </summary>
        public static string CreatePaymentDescription(int creditorId, int userId)
        {
            var encodedCreditorId = EncodeCreditorId(creditorId);
            var description = $"{_prefix}-{encodedCreditorId}-{userId}";

            // Nếu có suffix, thêm vào cuối để đánh dấu kết thúc
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                description += $"-{_suffix}";
            }

            return description;
        }

        /// <summary>
        /// Parse description để lấy creditorId và userId
        /// Trả về (creditorId, userId) nếu thành công, null nếu không đúng format
        /// Tìm chuỗi prefix trong description và parse từ đó, bỏ qua mọi ký tự trước và sau format
        /// Xử lý trường hợp ngân hàng thêm tiền tố/hậu tố vào description
        /// Nếu có suffix, sẽ tìm suffix để xác định chính xác phần cần parse
        /// </summary>
        public static (int creditorId, int userId)? ParsePaymentDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            // Tìm vị trí của prefix trong description (không phân biệt hoa thường)
            var descriptionUpper = description.ToUpperInvariant();
            var prefixUpper = _prefix.ToUpperInvariant();
            var prefixIndex = descriptionUpper.IndexOf(prefixUpper);

            if (prefixIndex < 0)
                return null;

            // Lấy phần description từ prefix trở đi
            var normalizedDescription = description.Substring(prefixIndex).Trim();

            // Nếu có suffix, tìm vị trí của suffix để xác định phần cần parse
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                var suffixUpper = _suffix.ToUpperInvariant();
                var suffixIndexInNormalized = normalizedDescription.ToUpperInvariant().IndexOf(suffixUpper);

                if (suffixIndexInNormalized > 0)
                {
                    // Tìm vị trí của suffix trong phần normalized
                    // Lấy phần từ prefix đến suffix (không bao gồm suffix)
                    normalizedDescription = normalizedDescription.Substring(0, suffixIndexInNormalized).Trim();
                }
            }

            // Split theo '-' để lấy các phần
            // Format: prefix-encodedCreditorId-userId[-suffix]
            var parts = normalizedDescription.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                return null;

            // Trim whitespace từ mỗi part để xử lý trường hợp có space
            var prefixPart = parts[0].Trim();
            var encodedCreditorIdPart = parts[1].Trim();
            var userIdPart = parts[2].Trim();

            // parts[0] phải là prefix (so sánh không phân biệt hoa thường)
            if (!string.Equals(prefixPart, _prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            // parts[1] là encodedCreditorId - decode nó
            var creditorId = DecodeCreditorId(encodedCreditorIdPart);
            if (!creditorId.HasValue)
                return null;

            // parts[2] là userId
            // Nếu có suffix và parts[2] chứa suffix, chỉ lấy phần trước suffix
            var userIdStr = userIdPart;

            // Nếu có suffix trong parts[2], loại bỏ nó
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                var suffixIndexInUserId = userIdStr.IndexOf(_suffix, StringComparison.OrdinalIgnoreCase);
                if (suffixIndexInUserId > 0)
                {
                    userIdStr = userIdStr.Substring(0, suffixIndexInUserId).Trim();
                }
            }

            // Parse userId (chỉ lấy số ở đầu, bỏ qua mọi text sau đó)
            // Ví dụ: "5 - QR" -> lấy "5"
            var userIdMatch = Regex.Match(userIdStr, @"^\d+");
            if (!userIdMatch.Success)
                return null;

            if (!int.TryParse(userIdMatch.Value, out int userId))
                return null;

            return (creditorId.Value, userId);
        }
    }
}

