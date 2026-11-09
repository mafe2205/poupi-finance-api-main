using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface INotificationsRepository
    {
        Task<Notification> GetByIdAsync(long id);
        Task<IEnumerable<Notification>> GetAllByUserIdAsync(long userId);
        Task<Notification> CreateAsync(CreateNotificationDto dto);
        Task<Notification> UpdateReadAtAsync(UpdateReadAt dto);
        Task<int> GetUnreadCountAsync(long userId);
        Task<bool> DeleteAsync(long id);
    }
}
