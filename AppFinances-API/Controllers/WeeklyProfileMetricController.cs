using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos;
using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeeklyProfileMetricController(IWeeklyProfileMetricService weeklyProfileMetricService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await weeklyProfileMetricService.GetByIdAsync(id));

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetWeeklyMetricsAsync(long userId, [FromQuery] string? startWeek, [FromQuery] string? endWeek)
            => Ok(await weeklyProfileMetricService.GetWeeklyMetricsAsync(userId, startWeek, endWeek));

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateWeeklyProfileMetricDto dto)
        {
            var created = await weeklyProfileMetricService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateWeeklyProfileMetricDto dto)
        {
            await weeklyProfileMetricService.UpdateAsync(dto);
            return NoContent();
        }
    }
}
