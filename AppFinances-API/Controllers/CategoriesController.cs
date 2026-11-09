using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Dtos;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Categories.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController(ICategoriesService service) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<CategoryResult>> Create(CreateCategoryDto dto)
            => Ok(await service.CreateAsync(dto));

        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryResult>> GetById(long id)
            => Ok(await service.GetByIdAsync(id));

        [HttpGet("user/{userId:long}")]
        public async Task<ActionResult<IEnumerable<CategoryResult>>> GetAllByUserId(long userId)
            => Ok(await service.GetAllByUserIdAsync(userId));

        [HttpGet("suggest")]
        public async Task<ActionResult<CategoryClassificationResult>> Suggest([FromQuery] string description)
            => Ok(await service.SuggestAsync(description));

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, UpdateCategoryDto dto)
        {
            await service.UpdateAsync(id, dto);
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
            => Ok(await service.DeleteAsync(id));

        [HttpGet("user/{userId:long}/inactive")]
        public async Task<ActionResult<IEnumerable<CategoryResult>>> GetAllInactiveByUserId(long userId)
            => Ok(await service.GetAllInactiveByUserIdAsync(userId));

        [HttpPatch("{id:long}/reactivate")]
        public async Task<ActionResult<CategoryResult>> Reactivate(long id)
            => Ok(await service.ReactivateAsync(id));
    }
}
