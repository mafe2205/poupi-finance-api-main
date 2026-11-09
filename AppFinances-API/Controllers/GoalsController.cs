using AppFinancesClassLibrary.Application.Goals.Entities.Dtos;
using AppFinancesClassLibrary.Application.Goals.Services;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController(IGoalsService goalsService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await goalsService.GetByIdAsync(id));

        [HttpGet("{id:long}/history")]
        public async Task<IActionResult> GetHistoryAsync(long id, [FromQuery] TransactionFilterDto filter)
            => Ok(await goalsService.GetHistoryAsync(id, filter));

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetAllByUserIdAsync(long userId)
            => Ok(await goalsService.GetAllByUserIdAsync(userId));

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateGoalDto dto)
            => Ok(await goalsService.CreateAsync(dto));

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateGoalDto dto)
            => Ok(await goalsService.UpdateAsync(dto));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await goalsService.DeleteAsync(id));
    }
}
