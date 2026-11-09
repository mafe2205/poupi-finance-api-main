using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IAuthenticationRepository
    {
        Task<Domain.Entities.Authentication> GetByIdAsync(long id);
        Task<Domain.Entities.Authentication> GetByUserIdAsync(long userId);
        Task UpdatePasswordAsync(long userId, string passwordHash);
        Task<Domain.Entities.Authentication> UpdateUsernameAsync(long id, string updatedUsername);
        Task UpdateUsernameByUserIdAsync(long userId, string username);
        Task<Domain.Entities.Authentication> CreateAsync(CreateAutheticationDto dto);
        Task<Domain.Entities.Authentication> GetByUsuarioOuEmailAsync(string userLoginName);
    }
}
