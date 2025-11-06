using System.ComponentModel.DataAnnotations;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Models.ViewModels
{
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

        // For dropdown
        public List<User>? AllUsers { get; set; }
    }
}

