using System.Data;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IDbConnection Connection { get; }
        IDbTransaction Transaction { get; }
        void Begin();
        Task<int> CommitAsync();
        Task RollbackAsync();
    }
}
