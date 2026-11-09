using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Transactions.Entities.Dtos
{
    public record SoftDeleteDto
    {
        [Required] public long Id { get; set; }
    }
}
