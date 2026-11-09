using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record CreateDocumentDto
    {
        [Required] public long UserId { get; set; }
        public long? TransactionId { get; set; }
        [Required] public string DocumentUrl { get; set; }
        [Required] public string TextOCR { get; set; } 
        [Required] public decimal TrustOCR { get; set; }
        [Required] public StatusOCRTypes Status { get; set; }
        public string IdempotencyKey { get; set; }
        public bool ConfirmedManually { get; set; } = false;
    }
}
