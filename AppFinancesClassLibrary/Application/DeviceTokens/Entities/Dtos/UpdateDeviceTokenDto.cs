using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos
{
    public record UpdateDeviceTokenDto
    {
        [Required] public long UserId { get; set; }
        [Required] public string Token { get; set; }
    }
}
