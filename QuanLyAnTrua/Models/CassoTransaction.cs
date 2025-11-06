namespace QuanLyAnTrua.Models
{
    public class CassoTransaction
    {
        public int Id { get; set; }
        public string? Reference { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public long RunningBalance { get; set; }
        public string? TransactionDateTime { get; set; }
        public string? AccountNumber { get; set; }
        public string? BankName { get; set; }
        public string? BankAbbreviation { get; set; }
        public string? VirtualAccountNumber { get; set; }
        public string? VirtualAccountName { get; set; }
        public string? CounterAccountName { get; set; }
        public string? CounterAccountNumber { get; set; }
        public string? CounterAccountBankId { get; set; }
        public string? CounterAccountBankName { get; set; }
        
        // Webhook cũ (deprecated)
        public string? Tid { get; set; }
        public long CusumBalance { get; set; }
        public string? When { get; set; }
        public string? BankSubAccId { get; set; }
        public string? SubAccId { get; set; }
        public string? VirtualAccount { get; set; }
        public string? VirtualAccountNameOld { get; set; }
        public string? CorresponsiveName { get; set; }
        public string? CorresponsiveAccount { get; set; }
        public string? CorresponsiveBankId { get; set; }
        public string? CorresponsiveBankName { get; set; }
    }
}

