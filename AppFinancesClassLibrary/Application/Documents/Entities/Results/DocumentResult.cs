using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Application.Documents.Services;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Results
{
    public record DocumentResult
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
        public bool NeedsManualReview { get; set; }
        public List<DocumentReviewReason> ReviewReasons { get; set; } = [];
        public string IdempotencyKey { get; set; }
        public ParsedDocumentResult Suggestion { get; set; }
        public List<DocumentItemResult> Items { get; set; } = [];
        public DateTime? OcrStartedAt { get; set; }
        public DateTime? OcrFinishedAt { get; set; }
        public int? EstablishmentCnae { get; set; }
        public string EstablishmentRegistryName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
