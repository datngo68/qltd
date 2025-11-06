using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyAnTrua.Models
{
    public class SharedReport
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Token")]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Loại báo cáo")]
        public string ReportType { get; set; } = "User"; // User, Group

        [Display(Name = "Người dùng")]
        public int? UserId { get; set; }

        [Display(Name = "Nhóm")]
        public int? GroupId { get; set; }

        [Display(Name = "Người tạo")]
        public int CreatedBy { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày hết hạn")]
        [DataType(DataType.DateTime)]
        public DateTime? ExpiresAt { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Lần truy cập cuối")]
        public DateTime? LastAccessedAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("GroupId")]
        public virtual Group? Group { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual User Creator { get; set; } = null!;
    }
}

