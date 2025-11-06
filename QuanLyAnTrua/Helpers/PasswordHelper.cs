using BCrypt.Net;
using System.Security.Cryptography;
using System.Text;

namespace QuanLyAnTrua.Helpers
{
    public static class PasswordHelper
    {
        /// <summary>
        /// Hash password sử dụng BCrypt (an toàn hơn MD5)
        /// </summary>
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        /// <summary>
        /// Hash password sử dụng MD5 (chỉ dùng để tương thích với dữ liệu cũ)
        /// </summary>
        private static string HashPasswordMD5(string password)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Verify password với hash đã lưu (hỗ trợ cả MD5 cũ và BCrypt mới)
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return false;

            try
            {
                // Kiểm tra nếu hash có vẻ là MD5 (32 ký tự hex), thì verify bằng MD5
                // Để tương thích với dữ liệu cũ
                if (hash.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-fA-F]{32}$"))
                {
                    string hashedPassword = HashPasswordMD5(password);
                    return hashedPassword.Equals(hash, StringComparison.OrdinalIgnoreCase);
                }

                // Nếu không phải MD5 format, dùng BCrypt
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}

