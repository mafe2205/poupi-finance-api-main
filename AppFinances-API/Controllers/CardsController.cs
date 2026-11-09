using AppFinancesClassLibrary.Application.Cards.Entities.Dtos;
using AppFinancesClassLibrary.Application.Cards.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardsController(ICardsService cardsService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await cardsService.GetByIdAsync(id));

        [HttpGet("bank-account/{bankAccountId:long}")]
        public async Task<IActionResult> GetAllByBankAccountIdAsync(long bankAccountId)
            => Ok(await cardsService.GetAllByBankAccountIdAsync(bankAccountId));

        [HttpGet("user/{userId:long}/inactive")]
        public async Task<IActionResult> GetAllInactiveByUserIdAsync(long userId)
            => Ok(await cardsService.GetAllInactiveByUserIdAsync(userId));

        [HttpPatch("{id:long}/reactivate")]
        public async Task<IActionResult> ReactivateAsync(long id)
            => Ok(await cardsService.ReactivateAsync(id));

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateCardDto dto)
            => Ok(await cardsService.CreateAsync(dto));

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateCardDto dto)
            => Ok(await cardsService.UpdateAsync(dto));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await cardsService.DeleteAsync(id));
    }
}
