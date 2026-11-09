using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;
using AppFinancesClassLibrary.Application.Login.Entities.Dtos;
using AppFinancesClassLibrary.Application.Login.Entities.Results;

namespace AppFinancesClassLibrary.Application.Authentication.Services
{
    public interface IAuthenticationService
    {
        Task<LoginResultDto> LoginAsync(LoginDto dto);
        Task<string> CheckLoginAsync(string userLoginName, string password);
        Task ChangePasswordAsync(ChangePasswordDto dto);
        Task<AuthenticationResult> UpdateUsernameAsync(long id, string updatedUsername);
        Task<AuthenticationResult> CreateAsync(CreateAutheticationDto dto);
    }
}
