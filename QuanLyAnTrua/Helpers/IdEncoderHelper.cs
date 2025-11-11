using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace QuanLyAnTrua.Helpers
{
    public static class IdEncoderHelper
    {
        private static string _prefix = "ThanToan"; // Default value
        private static string? _suffix = null; // Optional suffix
        private static string _separator = "-"; // Default separator (dấu -)

        /// <summary>
        /// Khởi tạo prefix, suffix và separator từ configuration
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

            var separator = configuration["Payment:DescriptionSeparator"];
            if (!string.IsNullOrWhiteSpace(separator))
            {
                _separator = separator.Trim();
            }
        }

        /// <summary>
        /// Reload prefix, suffix và separator từ configuration (dùng khi config thay đổi động)
        /// </summary>
        public static void Reload(IConfiguration configuration)
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
            else
            {
                _suffix = null; // Reset về null nếu không có trong config
            }

            var separator = configuration["Payment:DescriptionSeparator"];
            if (!string.IsNullOrWhiteSpace(separator))
            {
                _separator = separator.Trim();
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
        /// Lấy separator hiện tại
        /// </summary>
        public static string GetSeparator() => _separator;

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
        /// Tạo description cho thanh toán với format: {Prefix}{Base64UrlEncodedData}[{Suffix}]
        /// Trong đó Base64UrlEncodedData chứa (sau khi decode): {encodedCreditorId}-{userId}-{year}-{month}
        /// Lưu ý: Bên trong encoded data luôn dùng dấu "-" để phân tách
        /// Không sử dụng separator vì parse logic chỉ dựa vào prefix và suffix để xác định đoạn Base64
        /// Suffix được giữ ở ngoài (không mã hóa) để dễ nhận biết phần cuối
        /// Toàn bộ dữ liệu được mã hóa để tránh lộ liễu
        /// </summary>
        public static string CreatePaymentDescription(int creditorId, int userId, int year, int month)
        {
            var encodedCreditorId = EncodeCreditorId(creditorId);
            // Chỉ mã hóa phần data, không mã hóa suffix
            // Bên trong data luôn dùng dấu "-" để phân tách
            var data = $"{encodedCreditorId}-{userId}-{year}-{month}";

            // Mã hóa data bằng Base64Url
            var encodedData = EncodeBase64Url(data);

            // Format: {Prefix}{Base64UrlEncodedData}[{Suffix}]
            // Không cần separator vì parse logic chỉ dựa vào prefix và suffix
            // Suffix được giữ ở ngoài để dễ nhận biết phần cuối
            var description = $"{_prefix}{encodedData}";
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                description += _suffix;
            }

            return description;
        }

        /// <summary>
        /// Parse description để lấy creditorId, userId, year và month
        /// Trả về (creditorId, userId, year, month) nếu thành công, null nếu không đúng format
        /// Format: {Prefix}[Separator]{Base64UrlEncodedData}[Separator][{Suffix}]
        /// Trong đó Base64UrlEncodedData chứa (sau khi decode): {encodedCreditorId}-{userId}-{year}-{month}
        /// Logic: Chỉ dựa vào prefix và suffix để xác định đoạn Base64 cần parse
        /// Loại bỏ tất cả ký tự không phải Base64 (vì separator có thể bị ngân hàng xóa)
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

            // Tìm suffix (nếu có) để xác định phần cuối cần parse
            int? suffixIndex = null;
            if (!string.IsNullOrWhiteSpace(_suffix))
            {
                var suffixUpper = _suffix.ToUpperInvariant();
                var suffixIndexInNormalized = normalizedDescription.ToUpperInvariant().IndexOf(suffixUpper);
                if (suffixIndexInNormalized > _prefix.Length)
                {
                    suffixIndex = suffixIndexInNormalized;
                }
            }

            // Lấy phần giữa prefix và suffix (hoặc từ sau prefix đến hết nếu không có suffix)
            // Đây là phần có thể chứa Base64 data (có thể có separator hoặc không, có thể có ký tự ngân hàng thêm vào)
            var startIndex = _prefix.Length;
            var endIndex = suffixIndex ?? normalizedDescription.Length;
            var middlePart = normalizedDescription.Substring(startIndex, endIndex - startIndex);

            // Lọc chỉ lấy Base64Url characters (A-Z, a-z, 0-9, -, _)
            // Loại bỏ tất cả ký tự khác (separator, khoảng trắng, ký tự đặc biệt ngân hàng thêm vào)
            // Vì separator có thể bị ngân hàng xóa hoặc thay đổi, nên chỉ dựa vào prefix/suffix và Base64 characters
            var encodedDataBuilder = new StringBuilder();
            foreach (var ch in middlePart)
            {
                // Base64Url characters: A-Z, a-z, 0-9, -, _
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                    (ch >= '0' && ch <= '9') || ch == '-' || ch == '_')
                {
                    encodedDataBuilder.Append(ch);
                }
            }

            var encodedData = encodedDataBuilder.ToString().Trim();

            // Decode Base64Url để lấy data gốc
            var decodedData = DecodeBase64Url(encodedData);
            if (string.IsNullOrWhiteSpace(decodedData))
                return null;

            // Parse decoded data: {encodedCreditorId}-{userId}-{year}-{month}
            // Lưu ý: Bên trong decoded data luôn dùng dấu "-" để phân tách (không dùng separator từ config)
            // Vì separator từ config chỉ dùng ở mức ngoài (giữa prefix, encodedData, suffix)
            // Suffix không được mã hóa trong Base64Url, nên không có trong decodedData
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

