using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record CreateTransactionDto
    {
        [Required] public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public long? CardId { get; set; }
        public long? BankAccountId { get; set; }
        [Required] public string Description { get; set; }
        [Required] public PaymentMethod PaymentMethod { get; set; }
        public int PaymentParcels { get; set; } = 1;
        [Required] public int PaymentValueCents { get; set; }
        public bool Recorrent { get; set; }
        public int? RecorrentValue { get; set; }
        [Required] public DateOnly TransactionDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public DateOnly? ExpiringDate { get; set; }
    }
}
