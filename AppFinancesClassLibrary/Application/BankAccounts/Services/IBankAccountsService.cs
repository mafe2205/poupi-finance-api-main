using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;

namespace AppFinancesClassLibrary.Application.BankAccounts.Services
{
    public interface IBankAccountsService
    {
        Task<BankAccountResult> CreateAsync(CreateBankAccountDto dto);
        Task<BankAccountResult> UpdateAsync(UpdateBankAccountDto dto);
        Task<BankAccountResult> GetByIdAsync(long id);
        Task<IEnumerable<BankAccountResult>> GetAllByUserIdAsync(long userId);
        Task<BankAccountResult> GetByIdIncludingInactiveAsync(long id);
        Task<IEnumerable<BankAccountResult>> GetAllInactiveByUserIdAsync(long userId);
        Task<BankAccountResult> ReactivateAsync(long id);
        Task<BankAccountResult> DeleteAsync(long id);
        Task<BankAccountResult> SetBalanceAsync(long id, int balanceCents);
    }
}
