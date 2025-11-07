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
        /// Mã hóa chuỗi thành Base64Url (URL-safe)
        /// </summary>
        private static string EncodeBase64Url(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var base64 = Convert.ToBase64String(bytes);
            // Chuyển thành Base64Url (URL-safe)
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Giải mã Base64Url về chuỗi gốc
        /// </summary>
        private static string? DecodeBase64Url(string encoded)
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
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tạo description cho thanh toán với format: {Prefix}-{Base64UrlEncodedData}[-{Suffix}]
        /// Trong đó Base64UrlEncodedData chứa: {encodedCreditorId}-{userId}-{year}-{month}
        /// Suffix được giữ ở ngoài (không mã hóa) để dễ nhận biết phần cuối
        /// Toàn bộ dữ liệu được mã hóa để tránh lộ liễu
        /// </summary>
        public static string CreatePaymentDescription(int creditorId, int userId, int year, int month)
        {
            var encodedCreditorId = EncodeCreditorId(creditorId);
            // Chỉ mã hóa phần data, không mã hóa suffix
            var data = $"{encodedCreditorId}-{userId}-{year}-{month}";

            // Mã hóa data bằng Base64Url
            var encodedData = EncodeBase64Url(data);

            // Format: {Prefix}-{Base64UrlEncodedData}[-{Suffix}]
            // Suffix được giữ ở ngoài để dễ nhận biết phần cuối
            var description = $"{_prefix}-{encodedData}";
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                description += $"-{_suffix}";
            }

            return description;
        }

        /// <summary>
        /// Parse description để lấy creditorId, userId, year và month
        /// Trả về (creditorId, userId, year, month) nếu thành công, null nếu không đúng format
        /// Format mới: {Prefix}-{Base64UrlEncodedData}
        /// Trong đó Base64UrlEncodedData chứa: {encodedCreditorId}-{userId}-{year}-{month}[-{Suffix}]
        /// Xử lý trường hợp ngân hàng thêm tiền tố/hậu tố vào description
        /// </summary>
        public static (int creditorId, int userId, int year, int month)? ParsePaymentDescription(string? description)
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
            // Format: prefix-Base64UrlEncodedData[-suffix]
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                var suffixUpper = _suffix.ToUpperInvariant();
                var suffixIndexInNormalized = normalizedDescription.ToUpperInvariant().IndexOf(suffixUpper);

                if (suffixIndexInNormalized > 0)
                {
                    // Lấy phần từ prefix đến suffix (không bao gồm suffix)
                    normalizedDescription = normalizedDescription.Substring(0, suffixIndexInNormalized).Trim();
                }
            }

            // Split theo '-' để lấy prefix và encoded data
            // Format: prefix-Base64UrlEncodedData[-suffix]
            var parts = normalizedDescription.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

            // Cần ít nhất 2 parts: prefix và encoded data
            if (parts.Length < 2)
                return null;

            var prefixPart = parts[0].Trim();

            // parts[0] phải là prefix (so sánh không phân biệt hoa thường)
            if (!string.Equals(prefixPart, _prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            // parts[1] là Base64Url encoded data - decode nó
            // Có thể có thêm parts sau đó (ngân hàng thêm hậu tố), chỉ lấy parts[1]
            var encodedData = parts[1].Trim();

            // Decode Base64Url để lấy data gốc
            var decodedData = DecodeBase64Url(encodedData);
            if (string.IsNullOrWhiteSpace(decodedData))
                return null;

            // Parse decoded data: {encodedCreditorId}-{userId}-{year}-{month}
            // Lưu ý: Suffix không được mã hóa trong Base64Url, nên không có trong decodedData
            var dataParts = decodedData.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

            // Cần đúng 4 parts: encodedCreditorId, userId, year, month
            if (dataParts.Length < 4)
                return null;

            var encodedCreditorIdPart = dataParts[0].Trim();
            var userIdPart = dataParts[1].Trim();
            var yearPart = dataParts[2].Trim();
            var monthPart = dataParts[3].Trim();

            // Decode creditorId
            var creditorId = DecodeCreditorId(encodedCreditorIdPart);
            if (!creditorId.HasValue)
                return null;

            // Parse userId
            var userIdStr = userIdPart;

            // Parse userId (chỉ lấy số ở đầu, bỏ qua mọi text sau đó)
            var userIdMatch = Regex.Match(userIdStr, @"^\d+");
            if (!userIdMatch.Success)
                return null;

            if (!int.TryParse(userIdMatch.Value, out int userId))
                return null;

            // Parse year
            var yearStr = yearPart;

            var yearMatch = Regex.Match(yearStr, @"^\d+");
            if (!yearMatch.Success)
                return null;

            if (!int.TryParse(yearMatch.Value, out int year))
                return null;

            // Validate year (2000-2100)
            if (year < 2000 || year > 2100)
                return null;

            // Parse month
            var monthStr = monthPart;

            var monthMatch = Regex.Match(monthStr, @"^\d+");
            if (!monthMatch.Success)
                return null;

            if (!int.TryParse(monthMatch.Value, out int month))
                return null;

            // Validate month (1-12)
            if (month < 1 || month > 12)
                return null;

            return (creditorId.Value, userId, year, month);
        }
    }
}

