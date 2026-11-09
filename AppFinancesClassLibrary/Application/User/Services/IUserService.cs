using AppFinancesClassLibrary.Application.User.Entities.Dtos;
using AppFinancesClassLibrary.Application.User.Entities.Results;

namespace AppFinancesClassLibrary.Application.User.Services
{
    public interface IUserService
    {
        Task<UserResult> GetByIdAsync(long id);
        Task<UserResult> CreateAsync(CreateUserDto dto);
        Task<UserResult> UpdateAsync(long id, UpdateUserDto dto);
        Task<UserResult> DeleteAsync(long id);
    }
}
