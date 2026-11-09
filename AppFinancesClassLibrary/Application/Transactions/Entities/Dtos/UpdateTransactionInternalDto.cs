using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record UpdateTransactionInternalDto
    {
        public long Id { get; set; }
        public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public string Description { get; set; } = string.Empty;
        public int PaymentValueCents { get; set; }
        public DateOnly TransactionDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public bool CategoryConfirmedManually { get; set; } = true;
    }
}
