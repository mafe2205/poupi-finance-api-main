namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos
{
    public record CreateBankStatementPaymentInternalDto
    {
        public long BankStatementId { get; set; }
        public long BankAccountId { get; set; }
        public int AmountCents { get; set; }
        public DateOnly PaymentDate { get; set; }
    }
}
