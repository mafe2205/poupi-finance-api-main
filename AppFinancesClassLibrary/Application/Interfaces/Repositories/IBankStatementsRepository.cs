using AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IBankStatementsRepository
    {
        Task<BankStatement> GetByIdAsync(long id);
        Task<BankStatement> GetByCardIdAndMonthReferenceAsync(long cardId, string monthReference);
        Task<IEnumerable<BankStatement>> GetByCardIdAsync(long cardId);
        Task<BankStatement> CreateAsync(CreateBankStatementInternalDto dto);
        Task<BankStatement> MarkAsPaidAsync(long id, DateOnly paymentDate);
        Task<BankStatementAmounts> GetAmountsAsync(long id);
        Task<BankStatementPayment> CreatePaymentAsync(CreateBankStatementPaymentInternalDto dto);
        Task<IEnumerable<BankStatementPayment>> GetPaymentsByStatementIdAsync(long bankStatementId);
    }
}
