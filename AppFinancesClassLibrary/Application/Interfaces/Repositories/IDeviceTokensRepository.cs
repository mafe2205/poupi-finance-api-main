using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IDeviceTokensRepository
    {
        Task<DeviceToken> GetByIdAsync(long id);
        Task<DeviceToken> GetByUserIdAndTokenAsync(long userId, string token);
        Task<IEnumerable<DeviceToken>> GetByUserIdAsync(long userId);
        Task<DeviceToken> CreateAsync(CreateDeviceTokenDto dto);
        Task<DeviceToken> UpdateAsync(UpdateDeviceTokenDto dto);
        Task<bool> DeleteAsync(long id);
    }
}
