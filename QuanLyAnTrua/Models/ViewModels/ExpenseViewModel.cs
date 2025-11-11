using System.ComponentModel.DataAnnotations;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Models.ViewModels
{
    public enum SplitType
    {
        Equal,   // Chia đều
        Custom   // Nhập từng người
    }

    public class ExpenseViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Số tiền là bắt buộc")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
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

        [Required(ErrorMessage = "Vui lòng chọn ít nhất một người sử dụng")]
        [Display(Name = "người sử dụng")]
        public List<int> ParticipantIds { get; set; } = new List<int>();

        /// <summary>
        /// Cách chia chi phí: Equal (chia đều) hoặc Custom (nhập từng người)
        /// </summary>
        [Display(Name = "Cách chia")]
        public SplitType SplitType { get; set; } = SplitType.Equal;

        /// <summary>
        /// Dictionary lưu số tiền của từng participant (UserId -> Amount)
        /// Chỉ sử dụng khi SplitType = Custom
        /// </summary>
        [Display(Name = "Số tiền từng người")]
        public Dictionary<int, decimal> ParticipantAmounts { get; set; } = new Dictionary<int, decimal>();

        // For dropdown
        public List<User>? AllUsers { get; set; }
    }
}

