using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record CreateDocumentConfirmationDto
    {
        [Required] public long DocumentId { get; set; }
        [Required] public long TransactionId { get; set; }
        public string SuggestedDescription { get; set; }
        [Required] public string ConfirmedDescription { get; set; } = string.Empty;
        public int? SuggestedPaymentValueCents { get; set; }
        [Required] public int ConfirmedPaymentValueCents { get; set; }
        public DateOnly? SuggestedTransactionDate { get; set; }
        [Required] public DateOnly ConfirmedTransactionDate { get; set; }
        public long? SuggestedCategoryId { get; set; }
        [Required] public CategoryClassificationReason SuggestedCategoryReason { get; set; }
        [Required] public long ConfirmedCategoryId { get; set; }
        public int? SuggestedItemsTotalCents { get; set; }
        [Required] public bool ItemsConfirmed { get; set; }
        public string EstablishmentKey { get; set; }
        public DocumentDescriptionSource? SuggestedDescriptionSource { get; set; }
    }
}
