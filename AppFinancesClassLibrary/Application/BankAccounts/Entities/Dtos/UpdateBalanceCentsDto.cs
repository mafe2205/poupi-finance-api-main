using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos
{
    public record UpdateBalanceCentsDto
    {
        [Required] public long Id { get; set; }
        [Required] public int BalanceCents { get; set; } = 0;
    }
}
