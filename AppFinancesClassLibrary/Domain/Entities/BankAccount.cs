namespace AppFinancesClassLibrary.Domain.Entities
{
    public class BankAccount
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string BankName { get; set; }
        public string Agency { get; set; }
        public string AccountNumber { get; set; }
        public bool IsDefault { get; set; } = false;
        public int BalanceCents { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Active { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
    }
}
