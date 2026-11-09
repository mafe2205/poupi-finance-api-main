using AppFinances_API.Filters;
using AppFinancesClassLibrary.Application.User.Entities.Dtos;
using AppFinancesClassLibrary.Application.User.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController(IUserService userService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await userService.GetByIdAsync(id));

        [AllowAnonymous]
        [HttpPost]
        [UnitOfWorkTransaction]
        public async Task<IActionResult> CreateAsync([FromBody] CreateUserDto dto)
        {
            var created = await userService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
        }

        [HttpPut("{id:long}")]
        [UnitOfWorkTransaction]
        public async Task<IActionResult> UpdateAsync(long id, [FromBody] UpdateUserDto dto)
            => Ok(await userService.UpdateAsync(id, dto));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await userService.DeleteAsync(id));
    }
}
