namespace AppFinancesClassLibrary.Domain.Entities
{
    public class BankStatementPayment
    {
        public long Id { get; set; }
        public long BankStatementId { get; set; }
        public long BankAccountId { get; set; }
        public int AmountCents { get; set; }
        public DateOnly PaymentDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
