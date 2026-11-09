using AppFinancesClassLibrary.Application.Cards.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface ICardsRepository
    {
        Task<Card> GetByIdAsync(long id);
        Task<Card> GetByIdIncludingInactiveAsync(long id);
        Task<IEnumerable<Card>> GetAllByBankAccountIdAsync(long bankAccountId);
        Task<IEnumerable<Card>> GetAllInactiveByUserIdAsync(long userId);
        Task<Card> ReactivateAsync(long id);
        Task<Card> CreateAsync(CreateCardDto dto);
        Task<Card> UpdateAsync(UpdateCardDto dto);
        Task<Card> DeleteAsync(long id);
    }
}
