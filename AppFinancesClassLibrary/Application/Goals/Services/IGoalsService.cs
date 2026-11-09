using AppFinancesClassLibrary.Application.Goals.Entities.Dtos;
using AppFinancesClassLibrary.Application.Goals.Entities.Results;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Results;

namespace AppFinancesClassLibrary.Application.Goals.Services
{
    public interface IGoalsService
    {
        Task<GoalResult> CreateAsync(CreateGoalDto dto);
        Task<GoalResult> GetByIdAsync(long id);
        Task<IEnumerable<GoalResult>> GetAllByUserIdAsync(long userId);
        Task<IEnumerable<TransactionResult>> GetHistoryAsync(long goalId, TransactionFilterDto filter);
        Task<GoalResult> UpdateAsync(UpdateGoalDto dto);
        Task<GoalResult> DeleteAsync(long id);
    }
}
