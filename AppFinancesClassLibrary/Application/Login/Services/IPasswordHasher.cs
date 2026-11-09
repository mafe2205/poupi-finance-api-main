namespace AppFinancesClassLibrary.Application.Login.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool Verify(string password, string passwordHash);
    }
}
