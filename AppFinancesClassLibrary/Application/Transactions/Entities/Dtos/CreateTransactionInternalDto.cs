using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record CreateTransactionInternalDto
    {
        public long UserId { get; set; }
        public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public long? CardId { get; set; }
        public long? BankAccountId { get; set; }
        public long? BankStatementId { get; set; }
        public string Description { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public int PaymentParcels { get; set; }
        public int PaymentValueCents { get; set; }
        public int? PaymentValueOCR { get; set; }
        public string DescriptionOCR { get; set; }
        public bool Recorrent { get; set; }
        public int? RecorrentValue { get; set; }
        public long? RecorrentSerialId { get; set; }
        public DateOnly? TransactionDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public DateOnly? ExpiringDate { get; set; }
        public TransactionSource Source { get; set; }
        public bool CategoryConfirmedManually { get; set; } = true;
    }
}