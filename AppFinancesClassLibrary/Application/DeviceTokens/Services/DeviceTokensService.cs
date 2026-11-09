using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos;
using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.DeviceTokens.Services
{
    public class DeviceTokensService(IDeviceTokensRepository deviceTokensRepository, ICurrentUser currentUser) : IDeviceTokensService
    {
        private readonly IDeviceTokensRepository deviceTokensRepository = deviceTokensRepository;

        public async Task<long> RegisterAsync(CreateDeviceTokenDto dto)
        {
            Domain.Validate.NotNull(dto, nameof(CreateDeviceTokenDto));
            dto.UserId = currentUser.ResolveUserId(dto.UserId);
            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.String(dto.Token, nameof(dto.Token), minLength: 1, maxLength: 500);

            var existing = await deviceTokensRepository.GetByUserIdAndTokenAsync(dto.UserId, dto.Token);
            if (existing is not null)
            {
                var updateDto = new UpdateDeviceTokenDto
                {
                    UserId = dto.UserId,
                    Token = dto.Token,
                };
                Domain.Validate.NotNull(updateDto, nameof(UpdateDeviceTokenDto));
                Domain.Validate.Id(updateDto.UserId, nameof(updateDto.UserId));
                Domain.Validate.String(updateDto.Token, nameof(updateDto.Token), minLength: 1, maxLength: 500);

                var updated = await deviceTokensRepository.UpdateAsync(updateDto);
                return updated.Id;
            }

            Domain.Validate.Enum(dto.Platform, nameof(dto.Platform));

            var created = await deviceTokensRepository.CreateAsync(dto);
            return created.Id;
        }

        public async Task<DeviceTokenResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            var deviceToken = await deviceTokensRepository.GetByIdAsync(id)
                ?? throw new NotFoundException("Dispositivo não encontrado.");
            currentUser.EnsureCanAccess(deviceToken.UserId);

            return deviceToken.Adapt<DeviceTokenResult>();
        }

        public async Task<IEnumerable<DeviceTokenResult>> GetByUserIdAsync(long userId)
        {
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);

            var deviceTokens = await deviceTokensRepository.GetByUserIdAsync(userId);
            return deviceTokens.Adapt<IEnumerable<DeviceTokenResult>>();
        }

        public async Task<DeviceTokenResult> DeleteAsync(long id)
        {
            var existing = await GetByIdAsync(id);

            if (!await deviceTokensRepository.DeleteAsync(id))
                throw new NotFoundException("Dispositivo não encontrado.");

            return existing;
        }
    }
}
