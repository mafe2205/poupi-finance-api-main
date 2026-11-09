using AppFinances_API.Authorization;
using AppFinances_API.Middleware;
using AppFinancesClassLibrary.Application.Authentication.Services;
using AppFinancesClassLibrary.Application.BankAccounts.Services;
using AppFinancesClassLibrary.Application.BankStatements.Services;
using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Cards.Services;
using AppFinancesClassLibrary.Application.Categories.Services;
using AppFinancesClassLibrary.Application.DeviceTokens.Services;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Application.Goals.Services;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Login.Services;
using AppFinancesClassLibrary.Application.Notifications.Services;
using AppFinancesClassLibrary.Application.Options;
using AppFinancesClassLibrary.Application.Reports.Services;
using AppFinancesClassLibrary.Application.Transactions.Services;
using AppFinancesClassLibrary.Application.User.Services;
using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Services;
using AppFinancesClassLibrary.Infrastructure;
using AppFinancesClassLibrary.Infrastructure.BackgroundJobs;
using AppFinancesClassLibrary.Infrastructure.Data;
using AppFinancesClassLibrary.Infrastructure.Ocr;
using AppFinancesClassLibrary.Infrastructure.Ollama;
using AppFinancesClassLibrary.Infrastructure.CnpjRegistry;
using AppFinancesClassLibrary.Infrastructure.Push;
using AppFinancesClassLibrary.Infrastructure.Repositories;
using AppFinancesClassLibrary.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Data;
using System.Text;

DapperTypeHandlerRegistration.RegisterAll();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token retornado por POST /api/Authentication/login"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddScoped<DapperContext>();
builder.Services.AddScoped<IDbConnection>(sp =>
    sp.GetRequiredService<DapperContext>().CreateConnection());
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection("OcrSettings"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("OllamaSettings"));
builder.Services.Configure<CnpjLookupOptions>(builder.Configuration.GetSection("CnpjLookupSettings"));
builder.Services.Configure<InsightRuleOptions>(builder.Configuration.GetSection("InsightRuleSettings"));
builder.Services.Configure<ExpoPushOptions>(builder.Configuration.GetSection("ExpoPushSettings"));

builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBankAccountsRepository, BankAccountsRepository>();
builder.Services.AddScoped<IBankAccountsService, BankAccountsService>();
builder.Services.AddScoped<IBankStatementsRepository, BankStatementsRepository>();
builder.Services.AddScoped<IBankStatementsService, BankStatementsService>();
builder.Services.AddScoped<IBehavioralInsightsRepository, BehavioralInsightsRepository>();
builder.Services.AddScoped<IBehavioralInsightsService, BehavioralInsightsService>();
builder.Services.AddScoped<ICardsRepository, CardsRepository>();
builder.Services.AddScoped<ICardsService, CardsService>();
builder.Services.AddScoped<ICategoriesRepository, CategoriesRepository>();
builder.Services.AddScoped<ICategoriesService, CategoriesService>();
builder.Services.AddScoped<IDocumentsRepository, DocumentsRepository>();
builder.Services.AddScoped<IDocumentItemsRepository, DocumentItemsRepository>();
builder.Services.AddScoped<IDocumentConfirmationsRepository, DocumentConfirmationsRepository>();
builder.Services.AddScoped<ICnpjActivitiesRepository, CnpjActivitiesRepository>();
builder.Services.AddScoped<ICnpjActivityService, CnpjActivityService>();
builder.Services.AddScoped<IDocumentsService, DocumentsService>();
builder.Services.AddScoped<IDocumentParsingService, DocumentParsingService>();
builder.Services.AddSingleton<PaddleOcrEngine>();
builder.Services.AddScoped<IOcrService, PaddleOcrService>();
builder.Services.AddScoped<IDocumentFileStorage, LocalDocumentFileStorage>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IGoalsRepository, GoalsRepository>();
builder.Services.AddScoped<IGoalsService, GoalsService>();
builder.Services.AddScoped<ITransactionsRepository, TransactionsRepository>();
builder.Services.AddScoped<ITransactionsService, TransactionsService>();
builder.Services.AddScoped<IReportsRepository, ReportsRepository>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IWeeklyProfileMetricRepository, WeeklyProfileMetricRepository>();
builder.Services.AddScoped<IWeeklyProfileMetricService, WeeklyProfileMetricService>();
builder.Services.AddScoped<INotificationsRepository, NotificationsRepository>();
builder.Services.AddScoped<INotificationsService, NotificationsService>();
builder.Services.AddScoped<IDeviceTokensRepository, DeviceTokensRepository>();
builder.Services.AddScoped<IDeviceTokensService, DeviceTokensService>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

builder.Services.AddScoped<IInsightDataRepository, InsightDataRepository>();
builder.Services.AddScoped<IInsightTriggerService, InsightTriggerService>();
builder.Services.AddScoped<IInsightGenerationService, InsightGenerationService>();
builder.Services.AddScoped<IActionTakenBatchCalculator, ActionTakenBatchCalculator>();
builder.Services.AddScoped<IInsightRefreshService, InsightRefreshService>();

builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
{
    var ollamaOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(ollamaOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSeconds);
});

builder.Services.AddHttpClient<ICnpjRegistryClient, BrasilApiCnpjRegistryClient>((sp, client) =>
{
    var cnpjOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CnpjLookupOptions>>().Value;
    client.BaseAddress = new Uri(cnpjOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(cnpjOptions.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AppFinances-API");
});

builder.Services.AddHttpClient<IPushNotificationSender, ExpoPushNotificationSender>((sp, client) =>
{
    var expoOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExpoPushOptions>>().Value;
    client.BaseAddress = new Uri(expoOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(expoOptions.TimeoutSeconds);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    if (!string.IsNullOrWhiteSpace(expoOptions.AccessToken))
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expoOptions.AccessToken);
});

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

if (builder.Configuration.GetValue("BackgroundJobs:ScheduledJobsEnabled", true))
{
    builder.Services.AddHostedService<DailyInsightGenerationHostedService>();
    builder.Services.AddHostedService<WeeklyInsightGenerationHostedService>();
    builder.Services.AddHostedService<MonthlyInsightGenerationHostedService>();
    builder.Services.AddHostedService<ActionTakenEvaluationHostedService>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactNativePolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var header = context.Request.Headers.Authorization.ToString();
                if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = header["Bearer ".Length..].Trim();
                    while (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        token = token["Bearer ".Length..].Trim();
                    context.Token = token.Trim('"');
                }
                return Task.CompletedTask;
            },

            OnChallenge = async context =>
            {
                context.HandleResponse();

                var hasToken = !string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString());
                var error = context.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException expired => $"Token expirado em {expired.Expires:yyyy-MM-dd HH:mm:ss} UTC. Faça login novamente.",
                    not null => "Token inválido. Faça login novamente e use o token retornado.",
                    null when !hasToken => "Token ausente. Envie o header 'Authorization: Bearer <token>' (no Swagger, use o botão Authorize).",
                    _ => "Token inválido."
                };

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error }));
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("ReactNativePolicy");

app.UseMiddleware<DomainExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
