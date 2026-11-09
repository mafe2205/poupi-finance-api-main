using AppFinancesClassLibrary.Application.Cards.Entities.Dtos;
using AppFinancesClassLibrary.Application.Cards.Entities.Results;

namespace AppFinancesClassLibrary.Application.Cards.Services
{
    public interface ICardsService
    {
        Task<CardResult> CreateAsync(CreateCardDto dto);
        Task<CardResult> GetByIdAsync(long id);
        Task<IEnumerable<CardResult>> GetAllByBankAccountIdAsync(long bankAccountId);
        Task<CardResult> UpdateAsync(UpdateCardDto dto);
        Task<CardResult> DeleteAsync(long id);
        Task<IEnumerable<CardResult>> GetAllInactiveByUserIdAsync(long userId);
        Task<CardResult> ReactivateAsync(long id);
    }
}
