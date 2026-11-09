using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankAccounts.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankAccountsController(IBankAccountsService bankAccountService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await bankAccountService.GetByIdAsync(id));

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetAllByUserIdAsync(long userId)
            => Ok(await bankAccountService.GetAllByUserIdAsync(userId));

        [HttpGet("user/{userId:long}/inactive")]
        public async Task<IActionResult> GetAllInactiveByUserIdAsync(long userId)
            => Ok(await bankAccountService.GetAllInactiveByUserIdAsync(userId));

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateBankAccountDto dto)
            => Ok(await bankAccountService.CreateAsync(dto));

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateBankAccountDto dto)
            => Ok(await bankAccountService.UpdateAsync(dto));

        [HttpPatch("{id:long}/balance")]
        public async Task<IActionResult> SetBalanceAsync(long id, [FromBody] int balanceCents)
            => Ok(await bankAccountService.SetBalanceAsync(id, balanceCents));

        [HttpPatch("{id:long}/reactivate")]
        public async Task<IActionResult> ReactivateAsync(long id)
            => Ok(await bankAccountService.ReactivateAsync(id));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await bankAccountService.DeleteAsync(id));
    }
}
