using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Results
{
    public record BankStatementResult
    {
        public long Id { get; set; }
        public long CardId { get; set; }
        public string MonthReference { get; set; }
        public DateOnly ClosingDate { get; set; }
        public DateOnly DueDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public BankStatementsStatus Status { get; set; }
        public long TotalCents { get; set; }
        public long PaidCents { get; set; }
        public long RemainingCents { get; set; }
    }
}
