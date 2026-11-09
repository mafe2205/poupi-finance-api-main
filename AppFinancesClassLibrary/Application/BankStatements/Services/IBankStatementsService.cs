using AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Results;

namespace AppFinancesClassLibrary.Application.BankStatements.Services
{
    public interface IBankStatementsService
    {
        Task<BankStatementResult> CreateAsync(CreateBankStatementDto dto);
        Task<BankStatementResult> GetByIdAsync(long id);
        Task<BankStatementResult> UpdateAsync(long id, UpdateBankStatementDto dto);
        Task<BankStatementPaymentResult> PayAsync(long id, CreateBankStatementPaymentDto dto);
        Task<IEnumerable<BankStatementPaymentResult>> GetPaymentsAsync(long id);
    }
}
