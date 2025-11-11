using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Models.ViewModels
{
    public class MonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalExpenses { get; set; }
        public int TotalTransactions { get; set; }
        public List<UserDebtDetail> UserDebts { get; set; } = new List<UserDebtDetail>();
        public List<ExpenseDetail> Expenses { get; set; } = new List<ExpenseDetail>();
        public List<CreditorSummary> CreditorSummaries { get; set; } = new List<CreditorSummary>(); // Tổng hợp nợ theo người được nợ
        public int? CurrentUserId { get; set; } // ID của người đang xem (để hiển thị "Bạn cần trả cho")
    }

    public class UserDebtDetail
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; } // Tổng phải trả (khi là participant)
        public decimal PaidAmount { get; set; } // Tổng đã thanh toán
        public decimal PaidAsPayer { get; set; } // Tổng đã chi (khi là payer)
        public decimal ActualDebt => TotalAmount - PaidAsPayer; // Nợ thực tế = Phải trả - Đã chi
        public decimal RemainingAmount => ActualDebt - PaidAmount; // Còn lại sau khi đã thanh toán
        // Xử lý sai số làm tròn: nếu RemainingAmount <= 0.01 thì coi như đã thanh toán đầy đủ
        public bool IsFullyPaid => Math.Round(RemainingAmount, 2) <= 0;
        public List<PaymentDetail> Payments { get; set; } = new List<PaymentDetail>();
        public List<DebtDetail> DebtDetails { get; set; } = new List<DebtDetail>(); // Chi tiết ai nợ ai
        // Thông tin ngân hàng
        public string? BankName { get; set; }
        public string? BankAccount { get; set; }
        public string? AccountHolderName { get; set; }
        // Avatar
        public string? AvatarPath { get; set; }
    }

    public class PaymentDetail
    {
        public int Id { get; set; }
        public decimal PaidAmount { get; set; }
        public DateTime PaidDate { get; set; }
        public string? Notes { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty; // Người đã thanh toán
        public string? AvatarPath { get; set; }
    }

    public class DebtDetail
    {
        public int DebtorId { get; set; } // Người nợ
        public string DebtorName { get; set; } = string.Empty;
        public string? DebtorAvatarPath { get; set; } // Avatar người nợ
        public int CreditorId { get; set; } // Người được nợ
        public string CreditorName { get; set; } = string.Empty;
        public string? CreditorAvatarPath { get; set; } // Avatar người được nợ
        public decimal Amount { get; set; } // Số tiền nợ ban đầu
        public decimal RemainingAmount { get; set; } // Số tiền còn nợ sau khi trừ các khoản thanh toán
        public int ExpenseId { get; set; } // Từ expense nào
        public DateTime ExpenseDate { get; set; }
        public string? Description { get; set; }
    }

    public class CreditorSummary
    {
        public int CreditorId { get; set; } // Người được nợ
        public string CreditorName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; } // Tổng số tiền cần trả
        public List<DebtDetail> DebtDetails { get; set; } = new List<DebtDetail>(); // Chi tiết các khoản nợ
        // Thông tin ngân hàng của người được nợ
        public string? BankName { get; set; }
        public string? BankAccount { get; set; }
        public string? AccountHolderName { get; set; }
    }

    public class ExpenseDetail
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public int PayerId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public string? PayerAvatarPath { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? Description { get; set; }
        public List<string> ParticipantNames { get; set; } = new List<string>();
        public decimal AmountPerPerson { get; set; }
        /// <summary>
        /// Dictionary lưu số tiền của từng participant (UserId -> Amount)
        /// Nếu có thì là custom amounts, nếu không thì chia đều
        /// </summary>
        public Dictionary<int, decimal> ParticipantAmounts { get; set; } = new Dictionary<int, decimal>();
        /// <summary>
        /// Dictionary mapping UserId -> UserName để dễ tra cứu trong view
        /// </summary>
        public Dictionary<int, string> ParticipantIdToName { get; set; } = new Dictionary<int, string>();
        /// <summary>
        /// Dictionary mapping UserId -> AvatarPath để hiển thị avatar
        /// </summary>
        public Dictionary<int, string?> ParticipantIdToAvatarPath { get; set; } = new Dictionary<int, string?>();
    }
}

