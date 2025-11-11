using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace QuanLyAnTrua.Helpers
{
    public static class AvatarHelper
    {
        private const string DefaultAvatarPath = "/images/default-avatar.svg";

        /// <summary>
        /// Lấy URL avatar của user, trả về ảnh mặc định nếu chưa có
        /// </summary>
        public static string GetAvatarUrl(string? avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath))
            {
                return DefaultAvatarPath;
            }

            // Nếu là đường dẫn external (từ thư mục ngoài wwwroot), cần dùng controller để serve
            if (avatarPath.StartsWith("external:"))
            {
                // Lấy đường dẫn đầy đủ và encode để truyền qua URL
                var fullPath = avatarPath.Substring("external:".Length);
                var encodedPath = Uri.EscapeDataString(fullPath);
                return $"/Account/GetAvatar?path={encodedPath}";
            }

            // Đảm bảo đường dẫn bắt đầu bằng /
            if (!avatarPath.StartsWith("/"))
            {
                return "/" + avatarPath;
            }

            return avatarPath;
        }

        /// <summary>
        /// Validate file avatar (size và extension)
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateAvatarFile(
            IFormFile file,
            IConfiguration configuration)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "Vui lòng chọn file ảnh");
            }

            // Kiểm tra kích thước file
            var maxFileSize = configuration.GetValue<long>("Avatar:MaxFileSize", 3145728); // 3MB default
            if (file.Length > maxFileSize)
            {
                var maxSizeMB = maxFileSize / (1024.0 * 1024.0);
                return (false, $"Kích thước file không được vượt quá {maxSizeMB:F1}MB");
            }

            // Kiểm tra extension
            var allowedExtensions = configuration.GetValue<string>("Avatar:AllowedExtensions", "jpg,jpeg,png,gif,webp")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLower())
                .ToList() ?? new List<string> { "jpg", "jpeg", "png", "gif", "webp" };

            var fileExtension = Path.GetExtension(file.FileName)?.TrimStart('.').ToLower();
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                return (false, $"Định dạng file không hợp lệ. Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}");
            }

            return (true, null);
        }

        /// <summary>
        /// Lưu file avatar và trả về đường dẫn tương đối hoặc đường dẫn đầy đủ
        /// </summary>
        public static async Task<string?> SaveAvatar(
            IFormFile file,
            int userId,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var uploadPath = configuration.GetValue<string>("Avatar:UploadPath", "wwwroot/avatars");

            if (string.IsNullOrEmpty(uploadPath))
            {
                uploadPath = "wwwroot/avatars";
            }

            // Kiểm tra xem uploadPath có phải là đường dẫn tuyệt đối không
            // Windows drive path: F:/ hoặc F:\ hoặc có chứa : ở vị trí thứ 2
            bool isAbsolutePath = false;
            string uploadDir;

            // Normalize path: thay / thành \ cho Windows để kiểm tra
            var normalizedPathForCheck = uploadPath.Replace('/', '\\');

            // Kiểm tra nếu có chứa : ở vị trí thứ 2 (Windows drive path như F:, C:, D:)
            // Đây là cách chắc chắn nhất để nhận diện đường dẫn tuyệt đối trên Windows
            if (normalizedPathForCheck.Length >= 2 && normalizedPathForCheck[1] == ':')
            {
                isAbsolutePath = true;
                // Sử dụng normalized path với backslash cho Windows
                // Không cần Path.GetFullPath vì đã là đường dẫn đầy đủ
                uploadDir = normalizedPathForCheck;
            }
            else if (Path.IsPathRooted(normalizedPathForCheck))
            {
                isAbsolutePath = true;
                uploadDir = Path.GetFullPath(normalizedPathForCheck);
            }
            else
            {
                // Đường dẫn tương đối, combine với ContentRootPath
                uploadDir = Path.Combine(webHostEnvironment.ContentRootPath, uploadPath);
                uploadDir = Path.GetFullPath(uploadDir);
            }

            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            // Tạo tên file unique: userId_timestamp.extension
            var fileExtension = Path.GetExtension(file.FileName)?.TrimStart('.');
            var fileName = $"{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{fileExtension}";
            var filePath = Path.Combine(uploadDir, fileName);

            // Lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Trả về đường dẫn
            if (isAbsolutePath)
            {
                // Nếu là đường dẫn tuyệt đối, lưu đường dẫn đầy đủ với prefix đặc biệt để phân biệt
                // Sử dụng format: "external:{fullPath}" để biết đây là file ngoài wwwroot
                return $"external:{filePath}";
            }
            else
            {
                // Đường dẫn tương đối, trả về URL path
                var relativePath = (uploadPath ?? "wwwroot/avatars").Replace("wwwroot", "").Replace("\\", "/");
                if (!relativePath.StartsWith("/"))
                {
                    relativePath = "/" + relativePath;
                }
                return $"{relativePath}/{fileName}";
            }
        }

        /// <summary>
        /// Xóa file avatar cũ
        /// </summary>
        public static void DeleteAvatar(
            string? avatarPath,
            IWebHostEnvironment webHostEnvironment)
        {
            if (string.IsNullOrWhiteSpace(avatarPath))
            {
                return;
            }

            try
            {
                string fullPath;

                // Nếu là đường dẫn external
                if (avatarPath.StartsWith("external:"))
                {
                    fullPath = avatarPath.Substring("external:".Length);
                }
                else
                {
                    // Đường dẫn tương đối trong wwwroot
                    var cleanPath = avatarPath.TrimStart('/');
                    if (string.IsNullOrEmpty(webHostEnvironment.WebRootPath))
                    {
                        return;
                    }
                    fullPath = Path.Combine(webHostEnvironment.WebRootPath, cleanPath);

                    // Chỉ xóa file trong thư mục wwwroot để đảm bảo an toàn
                    if (!fullPath.StartsWith(webHostEnvironment.WebRootPath))
                    {
                        return;
                    }
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception)
            {
                // Ignore errors khi xóa file (có thể file đã bị xóa trước đó)
            }
        }
    }
}

