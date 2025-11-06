namespace QuanLyAnTrua.Models.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalExpenses { get; set; }
        public int TotalTransactions { get; set; }
        public string? TopPayerName { get; set; }
        public decimal TopPayerAmount { get; set; }
        
        public List<WeeklyStatistic> WeeklyStatistics { get; set; } = new List<WeeklyStatistic>();
        public List<MonthlyStatistic> MonthlyStatistics { get; set; } = new List<MonthlyStatistic>();
    }

    public class WeeklyStatistic
    {
        public int Week { get; set; }
        public int Year { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public List<UserDebt> UserDebts { get; set; } = new List<UserDebt>();
    }

    public class MonthlyStatistic
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public List<UserDebt> UserDebts { get; set; } = new List<UserDebt>();
    }

    public class UserDebt
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; } // Tổng phải trả (khi là participant)
        public decimal PaidAmount { get; set; } // Tổng đã thanh toán
        public decimal PaidAsPayer { get; set; } // Tổng đã chi (khi là payer)
        public decimal ActualDebt => TotalAmount - PaidAsPayer; // Nợ thực tế = Phải trả - Đã chi
        public decimal RemainingAmount => ActualDebt - PaidAmount; // Còn lại sau khi đã thanh toán
        public bool IsFullyPaid => RemainingAmount <= 0;
    }
}

