namespace AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos
{
    public record BankAccountResult
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string BankName { get; set; }
        public string Agency { get; set; }
        public string AccountNumber { get; set; }
        public bool IsDefault { get; set; }
        public int BalanceCents { get; set; }
        public bool Active { get; set; }
    }
}
