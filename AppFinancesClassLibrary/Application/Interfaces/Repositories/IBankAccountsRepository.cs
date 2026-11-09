using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IBankAccountsRepository
    {
        Task<BankAccount> GetByIdAsync(long id);
        Task<BankAccount> GetByIdIncludingInactiveAsync(long id);
        Task<IEnumerable<BankAccount>> GetAllInactiveByUserIdAsync(long userId);
        Task<BankAccount> ReactivateAsync(long id);
        Task<BankAccount> CreateAsync(CreateBankAccountDto bankAccount);
        Task<BankAccount> UpdateAsync(UpdateBankAccountDto bankAccount);
        Task<BankAccount> SetBalanceAsync(long id, int balanceCents);
        Task<BankAccount> AdjustBalanceAsync(long id, int deltaCents);
        Task<IEnumerable<BankAccount>> GetAllByUserIdAsync(long userId);
        Task<BankAccount> DeleteAsync(long id);
    }
}
