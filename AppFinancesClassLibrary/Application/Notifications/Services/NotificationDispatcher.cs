using AppFinancesClassLibrary.Application.DeviceTokens.Services;
using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Application.Notifications.Entities.Results;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Application.Notifications.Services
{
    public class NotificationDispatcher(
        INotificationsService notificationsService,
        IDeviceTokensService deviceTokensService,
        IPushNotificationSender pushNotificationSender,
        ILogger<NotificationDispatcher> logger) : INotificationDispatcher
    {
        public async Task<NotificationResult> DispatchAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default)
        {
            var notification = await notificationsService.CreateAsync(dto);

            try
            {
                var deviceTokens = (await deviceTokensService.GetByUserIdAsync(dto.UserId)).ToList();
                if (deviceTokens.Count == 0) return notification;

                var data = new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["type"] = dto.Type.ToString()
                };
                if (dto.RelatedEntityId.HasValue)
                    data["relatedEntityId"] = dto.RelatedEntityId.Value.ToString();

                var invalidTokens = await pushNotificationSender.SendAsync(
                    deviceTokens.Select(t => t.Token), dto.Title, dto.Message, data, cancellationToken);

                foreach (var deviceToken in deviceTokens.Where(t => invalidTokens.Contains(t.Token)))
                {
                    await deviceTokensService.DeleteAsync(deviceToken.Id);
                    logger.LogInformation("Token de dispositivo {DeviceTokenId} removido por não estar mais registrado.", deviceToken.Id);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao enviar push da notificação {NotificationId} para o usuário {UserId}", notification.Id, dto.UserId);
            }

            return notification;
        }
    }
}
