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

        // Navigation properties
        [ForeignKey("ExpenseId")]
        public virtual Expense Expense { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}

