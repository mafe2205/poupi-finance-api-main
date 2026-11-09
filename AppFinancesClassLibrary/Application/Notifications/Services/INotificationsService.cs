using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Application.Notifications.Entities.Results;

namespace AppFinancesClassLibrary.Application.Notifications.Services
{
    public interface INotificationsService
    {
        Task<NotificationResult> CreateAsync(CreateNotificationDto dto);
        Task<NotificationResult> GetByIdAsync(long id);
        Task<IEnumerable<NotificationResult>> GetAllByUserIdAsync(long userId);
        Task MarkAsReadAsync(long id);
        Task<int> GetUnreadCountAsync(long userId);
        Task<NotificationResult> DeleteAsync(long id);
    }
}
