using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Notifications.Entities.Results
{
    public record NotificationResult
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public long? RelatedEntityId { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
