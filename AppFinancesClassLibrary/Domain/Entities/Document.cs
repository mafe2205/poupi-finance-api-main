using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Domain.Entities
{
    public class Document
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long? TransactionId { get; set; }
        public string DocumentUrl { get; set; }
        public string TextOCR { get; set; }
        public decimal TrustOCR { get; set; }
        public decimal? ValueTrustOCR { get; set; }
        public StatusOCRTypes Status { get; set; }
        public bool ConfirmedManually { get; set; }
        public string IdempotencyKey { get; set; }
        public bool Active { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
        public DateTime? OcrStartedAt { get; set; }
        public DateTime? OcrFinishedAt { get; set; }
        public int? EstablishmentCnae { get; set; }
        public string EstablishmentRegistryName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
