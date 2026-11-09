using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Domain.Entities
{
    public class Transaction
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public long? CardId { get; set; }
        public long? BankAccountId { get; set; }
        public long? BankStatementId { get; set; }
        public string Description { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public int PaymentParcels { get; set; } = 1;
        public int PaymentValueCents { get; set; }
        public int? PaymentValueOCR { get; set; }
        public string DescriptionOCR { get; set; }
        public bool Recorrent { get; set; } = false;
        public int RecorrentValue { get; set; }
        public long? RecorrentSerialId { get; set; }
        public DateOnly TransactionDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public DateOnly? ExpiringDate { get; set; }
        public bool CategoryConfirmedManually { get; set; } = true;
        public bool Active { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
