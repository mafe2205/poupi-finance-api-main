using AppFinancesClassLibrary.Application.User.Entities.Dtos;
using DomainUser = AppFinancesClassLibrary.Domain.Entities.User;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<DomainUser> GetByIdAsync(long id);
        Task<DomainUser> GetByEmailAsync(string email);
        Task<IEnumerable<long>> GetAllActiveIdsAsync();
        Task<DomainUser> CreateAsync(CreateUserDto dto);
        Task<DomainUser> UpdateAsync(long id, UpdateUserDto dto);
        Task DeleteAsync(long id);
    }
}
