using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyAnTrua.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên người dùng là bắt buộc")]
        [Display(Name = "Tên người dùng")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Tên đăng nhập")]
        public string? Username { get; set; }

        [Display(Name = "Mật khẩu (Hash)")]
        public string? PasswordHash { get; set; }

        [Display(Name = "Vai trò")]
        public string Role { get; set; } = "User"; // SuperAdmin, Admin, User

        [Display(Name = "Nhóm")]
        public int? GroupId { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Tên ngân hàng")]
        public string? BankName { get; set; }

        [Display(Name = "Số tài khoản")]
        public string? BankAccount { get; set; }

        [Display(Name = "Tên chủ tài khoản")]
        public string? AccountHolderName { get; set; }

        [Display(Name = "Telegram User ID")]
        public string? TelegramUserId { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public string? AvatarPath { get; set; }

        // Navigation properties
        [ForeignKey("GroupId")]
        public virtual Group? Group { get; set; }
        public virtual ICollection<Expense> ExpensesAsPayer { get; set; } = new List<Expense>();
        public virtual ICollection<ExpenseParticipant> ExpenseParticipants { get; set; } = new List<ExpenseParticipant>();
        public virtual ICollection<MonthlyPayment> MonthlyPayments { get; set; } = new List<MonthlyPayment>();

        public bool IsSuperAdmin => Role == "SuperAdmin";
        public bool IsAdmin => Role == "Admin" || Role == "SuperAdmin";
    }
}

