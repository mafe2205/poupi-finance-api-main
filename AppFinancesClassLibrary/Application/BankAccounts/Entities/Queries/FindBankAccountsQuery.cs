using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankAccounts.Entities.Queries
{
    public record FindBankAccountsQuery
    {
        [Required] public long UserId { get; set; }
        [Required] public long Id { get; set; }
        public string BankName { get; set; }
        public string Agency { get; set; }
        public string AccountNumber { get; set; }
        public int BalanceCents { get; set; }
    }
}
