using AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Results;
using AppFinancesClassLibrary.Application.BankStatements.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankStatementsController(IBankStatementsService service) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<long>> Create(CreateBankStatementDto dto)
        {
            var created = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.Id);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BankStatementResult>> GetById(long id)
            => Ok(await service.GetByIdAsync(id));

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, UpdateBankStatementDto dto)
        {
            await service.UpdateAsync(id, dto);
            return NoContent();
        }

        [HttpPost("{id}/payments")]
        public async Task<ActionResult<BankStatementPaymentResult>> Pay(long id, CreateBankStatementPaymentDto dto)
        {
            var payment = await service.PayAsync(id, dto);
            return CreatedAtAction(nameof(GetPayments), new { id }, payment);
        }

        [HttpGet("{id}/payments")]
        public async Task<ActionResult<IEnumerable<BankStatementPaymentResult>>> GetPayments(long id)
            => Ok(await service.GetPaymentsAsync(id));
    }
}
