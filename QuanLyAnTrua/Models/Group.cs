using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyAnTrua.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên nhóm là bắt buộc")]
        [Display(Name = "Tên nhóm")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Người tạo")]
        public int CreatedBy { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("CreatedBy")]
        public virtual User Creator { get; set; } = null!;
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public virtual ICollection<MonthlyPayment> MonthlyPayments { get; set; } = new List<MonthlyPayment>();
    }
}

