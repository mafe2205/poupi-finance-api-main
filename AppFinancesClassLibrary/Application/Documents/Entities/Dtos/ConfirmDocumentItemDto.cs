using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record ConfirmDocumentItemDto
    {
        public long? SuggestedItemId { get; set; }
        public string Code { get; set; }
        [Required] public string Description { get; set; } = string.Empty;
        [Required] public decimal Quantity { get; set; }
        public string Unit { get; set; }
        [Required] public int UnitPriceCents { get; set; }
        [Required] public int TotalCents { get; set; }
    }
}
