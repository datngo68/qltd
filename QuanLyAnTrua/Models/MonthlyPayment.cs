using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyAnTrua.Models
{
    public class MonthlyPayment
    {
        public int Id { get; set; }

        [Display(Name = "Người dùng")]
        public int UserId { get; set; }

        [Display(Name = "Người được thanh toán")]
        public int? CreditorId { get; set; }

        [Required(ErrorMessage = "Năm là bắt buộc")]
        [Range(2000, 2100, ErrorMessage = "Năm không hợp lệ")]
        [Display(Name = "Năm")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Tháng là bắt buộc")]
        [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12")]
        [Display(Name = "Tháng")]
        public int Month { get; set; }

        [Required(ErrorMessage = "Số tiền là bắt buộc")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số tiền đã trả")]
        public decimal PaidAmount { get; set; }

        [Display(Name = "Ngày thanh toán")]
        [DataType(DataType.Date)]
        public DateTime PaidDate { get; set; } = DateTime.Today;

        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }

        [Display(Name = "Nhóm")]
        public int? GroupId { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Confirmed"; // Pending, Confirmed

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
        [ForeignKey("CreditorId")]
        public virtual User? Creditor { get; set; }
        [ForeignKey("GroupId")]
        public virtual Group? Group { get; set; }
    }
}

