using AppFinancesClassLibrary.Application.Login.Services;

namespace AppFinancesClassLibrary.Infrastructure.Security
{
    public class BCryptPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string senha)
            => BCrypt.Net.BCrypt.HashPassword(senha);

        public bool Verify(string senha, string senhaHash)
            => BCrypt.Net.BCrypt.Verify(senha, senhaHash);
    }
}
