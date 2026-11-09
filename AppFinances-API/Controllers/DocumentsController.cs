using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController(
        IDocumentsService documentsService,
        IDocumentUploadService documentUploadService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await documentsService.GetByIdAsync(id));

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetAllByUserIdAsync(long userId)
            => Ok(await documentsService.GetAllByUserIdAsync(userId));

        [HttpPost("upload")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadAsync(IFormFile file, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
        {
            using var stream = file.OpenReadStream();
            var result = await documentUploadService.UploadAsync(stream, idempotencyKey);
            return Ok(new { id = result.Id, created = result.Created });
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateDocumentDto dto)
        {
            var id = await documentsService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByIdAsync), new { id }, new { id });
        }

        [HttpPatch("{id:long}/transaction/{transactionId:long}")]
        public async Task<IActionResult> LinkTransactionAsync(long id, long transactionId)
            => Ok(await documentsService.LinkTransactionAsync(id, transactionId));

        [HttpPatch("{id:long}/status")]
        public async Task<IActionResult> UpdateStatusAsync(long id, [FromBody] StatusOCRTypes status)
            => Ok(await documentsService.UpdateStatusAsync(id, status));

        [HttpPatch("{id:long}/confirm")]
        public async Task<IActionResult> ConfirmManuallyAsync(long id, [FromBody] bool confirmedManually)
            => Ok(await documentsService.ConfirmManuallyAsync(id, confirmedManually));

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await documentsService.DeleteAsync(id));
    }
}
