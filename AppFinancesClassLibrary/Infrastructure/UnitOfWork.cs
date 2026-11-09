using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using System.Data;

namespace AppFinancesClassLibrary.Infrastructure
{
    public class UnitOfWork(IDbConnection connection) : IUnitOfWork
    {
        private readonly IDbConnection connection = connection;
        private IDbTransaction transaction;

        public IDbConnection Connection => connection;
        public IDbTransaction Transaction => transaction;

        public void Begin()
        {
            if (transaction is not null) return;

            if (connection.State != ConnectionState.Open) connection.Open();

            transaction = connection.BeginTransaction();
        }

        public Task<int> CommitAsync()
        {
            if (transaction is null) return Task.FromResult(0);

            try
            {
                transaction.Commit();
                return Task.FromResult(1);
            }
            finally
            {
                transaction.Dispose();
                transaction = null;
            }
        }

        public Task RollbackAsync()
        {
            if (transaction is null) return Task.CompletedTask;

            try
            {
                transaction.Rollback();
                return Task.CompletedTask;
            }
            finally
            {
                transaction.Dispose();
                transaction = null;
            }
        }

        public void Dispose()
        {
            transaction?.Dispose();
            connection?.Dispose();
        }
    }
}