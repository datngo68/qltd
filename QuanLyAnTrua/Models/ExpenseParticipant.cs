using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyAnTrua.Models
{
    public class ExpenseParticipant
    {
        [Key]
        [Column(Order = 0)]
        public int ExpenseId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int UserId { get; set; }

        /// <summary>
        /// Số tiền của participant này. Nếu null thì chia đều.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số tiền")]
        public decimal? Amount { get; set; }

        // Navigation properties
        [ForeignKey("ExpenseId")]
        public virtual Expense Expense { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}

