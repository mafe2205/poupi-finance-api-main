namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Results
{
    public record BankStatementAmounts
    {
        public long TotalCents { get; set; }
        public long PaidCents { get; set; }
    }
}
