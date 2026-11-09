namespace AppFinancesClassLibrary.Application.Login.Services
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) GenerateToken(long userId, string role);
    }
}
