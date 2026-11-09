using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos
{
    public record UpdateBankAccountDto
    {
        [Required] public long Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string Agency { get; set; } = string.Empty;
        public string AccountNumber { get; set; }
    }
}
