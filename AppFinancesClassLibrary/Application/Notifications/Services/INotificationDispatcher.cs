using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Application.Notifications.Entities.Results;

namespace AppFinancesClassLibrary.Application.Notifications.Services
{
    public interface INotificationDispatcher
    {
        Task<NotificationResult> DispatchAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default);
    }
}
