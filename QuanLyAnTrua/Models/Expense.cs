using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyAnTrua.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Số tiền là bắt buộc")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số tiền")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Người chi tiền là bắt buộc")]
        [Display(Name = "Người chi tiền")]
        public int PayerId { get; set; }

        [Required(ErrorMessage = "Ngày chi là bắt buộc")]
        [Display(Name = "Ngày chi")]
        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; } = DateTime.Today;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Nhóm")]
        public int? GroupId { get; set; }

        // Navigation properties
        [ForeignKey("PayerId")]
        public virtual User Payer { get; set; } = null!;
        [ForeignKey("GroupId")]
        public virtual Group? Group { get; set; }
        public virtual ICollection<ExpenseParticipant> Participants { get; set; } = new List<ExpenseParticipant>();
    }
}

