namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record TransactionFilterDto
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public long? CategoryId { get; set; }
        public long? CardId { get; set; }
        public long? BankAccountId { get; set; }
        public long? MetaId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
