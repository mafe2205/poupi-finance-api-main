using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos
{
    public record CreateDeviceTokenDto
    {
        [Required] public long UserId { get; set; }
        [Required] public string Token { get; set; }
        [Required] public DevicePlatform Platform { get; set; }
    }
}
