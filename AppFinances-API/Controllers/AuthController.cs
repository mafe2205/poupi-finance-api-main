using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;
using AppFinancesClassLibrary.Application.Login.Entities.Dtos;
using AppFinancesClassLibrary.Application.Login.Entities.Results;
using AppFinancesClassLibrary.Application.Authentication.Services;
using AppFinances_API.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [UnitOfWorkTransaction]
    public class AuthenticationController(IAuthenticationService service) : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResultDto>> Login(LoginDto dto)
            => Ok(await service.LoginAsync(dto));

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            await service.ChangePasswordAsync(dto);
            return NoContent();
        }
    }
}
