using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos;
using AppFinancesClassLibrary.Application.DeviceTokens.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceTokensController(IDeviceTokensService deviceTokensService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> RegisterAsync([FromBody] CreateDeviceTokenDto dto)
            => Ok(new { id = await deviceTokensService.RegisterAsync(dto) });

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetByUserIdAsync(long userId)
            => Ok(await deviceTokensService.GetByUserIdAsync(userId));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await deviceTokensService.DeleteAsync(id));
    }
}
