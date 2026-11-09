namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Results
{
    public record BankStatementPaymentResult
    {
        public long Id { get; set; }
        public long BankStatementId { get; set; }
        public long BankAccountId { get; set; }
        public int AmountCents { get; set; }
        public DateOnly PaymentDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
