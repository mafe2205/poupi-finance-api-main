using AppFinancesClassLibrary.Application.Reports.Entities.Results;
using AppFinancesClassLibrary.Application.Reports.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController(IReportsService reportsService) : ControllerBase
    {
        [HttpGet("summary")]
        public async Task<ActionResult<ReportSummaryResult>> GetSummary([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
            => Ok(await reportsService.GetSummaryAsync(startDate, endDate));
    }
}
