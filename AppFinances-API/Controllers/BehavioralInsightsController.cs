using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos;
using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Results;
using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BehavioralInsightsController(IBehavioralInsightsService service, IInsightRefreshService refreshService) : ControllerBase
    {
        [HttpPost("refresh")]
        public async Task<ActionResult<IReadOnlyList<OutdatedInsight>>> Refresh(CancellationToken cancellationToken)
            => Ok(await refreshService.RefreshCurrentUserAsync(cancellationToken));

        [HttpPost]
        public async Task<ActionResult<long>> Create(CreateBehavioralInsightDto dto)
        {
            var id = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BehavioralInsightResult>> GetById(long id)
            => Ok(await service.GetByIdAsync(id));

        [HttpGet("user/{userId:long}")]
        public async Task<ActionResult<IEnumerable<BehavioralInsightResult>>> GetAllByUserId(long userId, [FromQuery] bool includeOutdated = false)
            => Ok(await service.GetAllByUserIdAsync(userId, includeOutdated));

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, UpdateBehavioralInsightDto dto)
        {
            await service.UpdateAsync(id, dto);
            return NoContent();
        }

        [HttpPatch("{id}/feedback")]
        public async Task<IActionResult> UpdateFeedback(long id, UpdateBehavioralInsightFeedbackDto dto)
        {
            await service.UpdateFeedbackAsync(id, dto);
            return NoContent();
        }

        [HttpPost("{id}/mark-viewed")]
        public async Task<IActionResult> MarkViewed(long id)
        {
            await service.MarkViewedAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/evaluate-action-taken")]
        public async Task<IActionResult> EvaluateActionTaken(long id)
        {
            await service.EvaluateActionTakenAsync(id);
            return NoContent();
        }
    }
}
