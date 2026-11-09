using AppFinancesClassLibrary.Application.Goals.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IGoalsRepository
    {
        Task<Goal> GetByIdAsync(long id);
        Task<IEnumerable<Goal>> GetAllByUserIdAsync(long userId);
        Task<Goal> CreateAsync(CreateGoalDto dto);
        Task<Goal> UpdateAsync(UpdateGoalDto dto);
        Task<Goal> DeleteAsync(long id);
    }
}
