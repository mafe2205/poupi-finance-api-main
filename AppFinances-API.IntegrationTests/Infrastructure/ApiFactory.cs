using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Application.Notifications.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AppFinances_API.IntegrationTests.Infrastructure
{
    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("appfinances")
            .Build();

        private readonly string storagePath = Path.Combine(Path.GetTempPath(), "appfinances-integration-tests", Guid.NewGuid().ToString("N"));

        public FixedOllamaClient Ollama { get; } = new();

        public async Task InitializeAsync()
        {
            await database.StartAsync();

            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "init.sql"));
            await using var connection = new NpgsqlConnection(database.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTests");

            builder.UseSetting("ConnectionStrings:PostgresConnection", database.GetConnectionString());
            builder.UseSetting("Jwt:Key", Convert.ToBase64String(Enumerable.Range(1, 64).Select(i => (byte)i).ToArray()));
            builder.UseSetting("BackgroundJobs:ScheduledJobsEnabled", "false");
            builder.UseSetting("OcrSettings:StorageBasePath", storagePath);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOllamaClient>();
                services.AddSingleton<IOllamaClient>(Ollama);

                services.RemoveAll<IPushNotificationSender>();
                services.AddSingleton<IPushNotificationSender, NoPushNotificationSender>();

                if (Environment.GetEnvironmentVariable(EvaluationFactAttribute.Variable) != "1")
                {
                    services.RemoveAll<ICnpjRegistryClient>();
                    services.AddSingleton<ICnpjRegistryClient, NoCnpjRegistryClient>();
                }
            });
        }

        public new async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await database.DisposeAsync();

            try { Directory.Delete(storagePath, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [CollectionDefinition(Name)]
    public class ApiCollection : ICollectionFixture<ApiFactory>
    {
        public const string Name = "API com banco descartável";
    }
}
