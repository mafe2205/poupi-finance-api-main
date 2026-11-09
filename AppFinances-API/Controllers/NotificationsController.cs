using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Application.Notifications.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController(INotificationsService notificationsService) : ControllerBase
    {
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(long id)
            => Ok(await notificationsService.GetByIdAsync(id));

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetAllByUserIdAsync(long userId)
            => Ok(await notificationsService.GetAllByUserIdAsync(userId));

        [HttpGet("user/{userId:long}/unread-count")]
        public async Task<IActionResult> GetUnreadCountAsync(long userId)
            => Ok(new { count = await notificationsService.GetUnreadCountAsync(userId) });

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateNotificationDto dto)
        {
            var created = await notificationsService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
        }

        [HttpPatch("{id:long}/read")]
        public async Task<IActionResult> MarkAsReadAsync(long id)
        {
            await notificationsService.MarkAsReadAsync(id);
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteAsync(long id)
            => Ok(await notificationsService.DeleteAsync(id));
    }
}
