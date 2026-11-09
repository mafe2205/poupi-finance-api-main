using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record UpdateTransactionDto
    {
        public string Description { get; set; }
        public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public int PaymentValueCents { get; set; }
        public DateOnly TransactionDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
    }
}
