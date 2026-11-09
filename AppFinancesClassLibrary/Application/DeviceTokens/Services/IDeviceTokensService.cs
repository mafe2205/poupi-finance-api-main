using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos;
using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Results;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppFinancesClassLibrary.Application.DeviceTokens.Services
{
    public interface IDeviceTokensService
    {
        Task<long> RegisterAsync(CreateDeviceTokenDto dto);
        Task<DeviceTokenResult> GetByIdAsync(long id);
        Task<IEnumerable<DeviceTokenResult>> GetByUserIdAsync(long userId);
        Task<DeviceTokenResult> DeleteAsync(long id);
    }
}
