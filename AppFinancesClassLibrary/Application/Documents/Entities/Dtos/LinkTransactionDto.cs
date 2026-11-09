using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Documents.Entities.Dtos
{
    public record LinkTransactionDto
    {
        [Required] public long Id { get; set; }
        [Required] public long TransactionId { get; set; }
    }
}
