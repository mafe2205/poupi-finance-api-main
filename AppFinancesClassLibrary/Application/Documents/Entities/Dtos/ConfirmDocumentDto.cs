using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record ConfirmDocumentDto
    {
        [Required] public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public long? CardId { get; set; }
        public long? BankAccountId { get; set; }
        [Required] public string Description { get; set; } = string.Empty;
        [Required] public int PaymentValueCents { get; set; }
        [Required] public PaymentMethod PaymentMethod { get; set; }
        [Required] public DateOnly TransactionDate { get; set; }
        public DateOnly? ExpiringDate { get; set; }
        public List<ConfirmDocumentItemDto> Items { get; set; }
    }
}
