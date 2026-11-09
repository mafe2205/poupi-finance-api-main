using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/transactions")]
    public class TransactionsController(ITransactionsService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetByUser([FromQuery] TransactionFilterDto filter)
            => Ok(await service.GetByUserAsync(filter));

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
            => Ok(await service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto)
            => Ok(await service.CreateAsync(dto));

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateTransactionDto dto)
            => Ok(await service.UpdateAsync(id, dto));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
            => Ok(await service.DeleteAsync(id));

        [HttpPost("from-document/{documentId:long}")]
        public async Task<IActionResult> CreateFromDocument(long documentId, [FromBody] ConfirmDocumentDto dto)
            => Ok(await service.CreateFromDocumentAsync(documentId, dto));

        [HttpPost("reclassify")]
        public async Task<IActionResult> Reclassify()
            => Ok(await service.ReclassifyAsync());

        [HttpPatch("{id:long}/cancel-series")]
        public async Task<IActionResult> CancelSeries(long id)
        {
            await service.CancelSeriesAsync(id);
            return NoContent();
        }
    }
}
