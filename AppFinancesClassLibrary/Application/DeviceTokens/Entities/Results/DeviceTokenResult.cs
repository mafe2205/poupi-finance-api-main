using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.DeviceTokens.Entities.Results
{
    public record DeviceTokenResult
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Token { get; set; }
        public DevicePlatform Platform { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
