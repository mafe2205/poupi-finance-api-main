using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Application.Notifications.Entities.Results;
using AppFinancesClassLibrary.Application.User.Entities.Results;
using AppFinancesClassLibrary.Application.User.Services;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.Notifications.Services
{
    public class NotificationsService(IUserService userService, INotificationsRepository notificationsRepository, ICurrentUser currentUser) : INotificationsService
    {
        private readonly INotificationsRepository notificationsRepository = notificationsRepository;
        private readonly IUserService userService = userService;

        public async Task<NotificationResult> CreateAsync(CreateNotificationDto dto)
        {
            UserResult userFound;
            Notification notification;

            Domain.Validate.NotNull(dto, nameof(CreateNotificationDto));

            dto.UserId = currentUser.ResolveUserId(dto.UserId);

            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.Enum(dto.Type, nameof(dto.Type));
            Domain.Validate.String(dto.Title, nameof(dto.Title), minLength: 2, maxLength: 100);
            Domain.Validate.String(dto.Message, nameof(dto.Message), minLength: 2, maxLength: 500);

            if (dto.RelatedEntityId.HasValue)
                Domain.Validate.Id(dto.RelatedEntityId.Value, nameof(dto.RelatedEntityId));

            userFound = await userService.GetByIdAsync(dto.UserId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            notification = await notificationsRepository.CreateAsync(dto) ?? throw new DomainValidationException("Erro ao criar notificação.");
            return notification.Adapt<NotificationResult>();
        }

        public async Task<NotificationResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            Notification notification = await GetOwnedAsync(id);
            return notification.Adapt<NotificationResult>();
        }

        public async Task<IEnumerable<NotificationResult>> GetAllByUserIdAsync(long userId)
        {
            UserResult userFound;
            IEnumerable<Notification> notifications;

            Domain.Validate.Id(userId, nameof(userId));

            userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            notifications = await notificationsRepository.GetAllByUserIdAsync(userId) ?? [];
            return notifications.Adapt<IEnumerable<NotificationResult>>();
        }

        public async Task MarkAsReadAsync(long id)
        {
            UpdateReadAt dto;
            Notification notification;

            Domain.Validate.Id(id, nameof(id));

            notification = await GetOwnedAsync(id);

            dto = new()
            {
                Id = notification.Id,
                ReadAt = DateTime.UtcNow,
            };
            await notificationsRepository.UpdateReadAtAsync(dto);
        }

        public async Task<int> GetUnreadCountAsync(long userId)
        {
            UserResult userFound;

            Domain.Validate.Id(userId, nameof(userId));

            userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException("Usuário não encontrado.");

            return await notificationsRepository.GetUnreadCountAsync(userId);
        }

        public async Task<NotificationResult> DeleteAsync(long id)
        {
            NotificationResult notification = await GetByIdAsync(id);

            if (!await notificationsRepository.DeleteAsync(id))
                throw new NotFoundException("Notificação não encontrada.");

            return notification;
        }

        private async Task<Notification> GetOwnedAsync(long id)
        {
            Notification notification = await notificationsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Notificação não encontrada.");
            currentUser.EnsureCanAccess(notification.UserId);

            return notification;
        }
    }
}
