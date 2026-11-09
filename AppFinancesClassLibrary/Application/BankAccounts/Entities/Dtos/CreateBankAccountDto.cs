using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos
{
    public record CreateBankAccountDto
    {
        [Required] public long UserId { get; set; }
        [Required] public string BankName { get; set; } = string.Empty;
        [Required] public string Agency { get; set; } = string.Empty;
        [Required] public string AccountNumber { get; set; }
        [Required] public bool IsDefault { get; set; } = false;
        [Required] public int BalanceCents { get; set; } = 0;
    }
}
