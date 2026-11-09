using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AppFinancesClassLibrary.Infrastructure.Data;

public class DapperContext
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("PostgresConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgresConnection não configurada.");
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}