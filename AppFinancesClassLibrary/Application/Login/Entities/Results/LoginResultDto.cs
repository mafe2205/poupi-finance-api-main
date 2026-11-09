namespace AppFinancesClassLibrary.Application.Login.Entities.Results
{
    public record LoginResultDto
    {
        public string Token { get; set; } = string.Empty;
        public long UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
