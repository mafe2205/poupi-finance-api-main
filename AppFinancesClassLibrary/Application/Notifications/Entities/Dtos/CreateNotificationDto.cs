using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Notifications.Entities.Dtos
{
    public record CreateNotificationDto
    {
        [Required] public long UserId { get; set; }
        [Required] public NotificationType Type { get; set; }
        [Required] public string Title { get; set; } = string.Empty;
        [Required] public string Message { get; set; } = string.Empty;
        public long? RelatedEntityId { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
