using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Interfaces.Services;
using USP.Core.Interfaces.Services.Secrets;
using USP.Infrastructure.Authorization;
using USP.Infrastructure.Middleware;
using USP.Infrastructure.Persistence;
using USP.Infrastructure.Services;
using USP.Infrastructure.Services.Secrets;
using USP.Shared.Configuration.Options;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "USP")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.WithProperty("MachineName", Environment.MachineName)
        .WriteTo.Console(new ElasticsearchJsonFormatter())
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(context.Configuration["Observability:ElasticsearchUri"] ?? "http://localhost:9200"))
        {
            IndexFormat = context.Configuration["Observability:LogIndexFormat"] ?? "usp-logs-{0:yyyy.MM.dd}",
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog,
            FailureCallback = e => Console.WriteLine($"Unable to submit event: {e.MessageTemplate}"),
            MinimumLogEventLevel = context.HostingEnvironment.IsProduction()
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information
        });
});

// Configuration
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection("Redis"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection("Email"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EncryptionOptions>()
    .Bind(builder.Configuration.GetSection("Encryption"))
    .ValidateOnStart()
    .PostConfigure<IHostEnvironment>((options, env) =>
    {
        options.Validate();

        if (options.AutoGenerateKeyFile && env.IsProduction())
        {
            throw new InvalidOperationException(
                "ENCRYPTION__AUTO_GENERATE_KEY_FILE must be false in production environments. " +
                "Master keys must be explicitly provided via environment variables or secure file storage.");
        }
    });

builder.Services.AddSingleton<IMasterKeyProvider, MasterKeyProvider>();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString!, npgsql =>
    {
        npgsql.CommandTimeout(30);
        npgsql.MigrationsAssembly("USP.Infrastructure");
    });
});

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Repositories
builder.Services.AddScoped<USP.Core.Interfaces.Repositories.ISessionRepository, USP.Infrastructure.Repositories.SessionRepository>();

// Authentication & Security Services
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.ITokenService, USP.Infrastructure.Services.Authentication.TokenService>();
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.IPasswordService, USP.Infrastructure.Services.Authentication.PasswordService>();
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.ISessionService, USP.Infrastructure.Services.Authentication.SessionService>();
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.IAuthenticationService, USP.Infrastructure.Services.Authentication.AuthenticationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// MFA Services
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.ITOTPService, USP.Infrastructure.Services.Authentication.TOTPService>();
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.IBackupCodesService, USP.Infrastructure.Services.Authentication.BackupCodesService>();
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authentication.IMFAService, USP.Infrastructure.Services.Authentication.MFAService>();

// Secrets Management Services
builder.Services.AddScoped<USP.Core.Interfaces.Services.Secrets.IEncryptionService, USP.Infrastructure.Services.Secrets.EncryptionService>();
builder.Services.AddScoped<USP.Core.Interfaces.Services.Secrets.ISecretService, USP.Infrastructure.Services.Secrets.SecretService>();

// Authorization Services
builder.Services.AddScoped<USP.Core.Interfaces.Services.Authorization.IAuthorizationService, USP.Infrastructure.Services.Authorization.AuthorizationService>();

// Permission-based Authorization (custom policy provider and handlers)
builder.Services.AddPermissionBasedAuthorization();

// Audit Services
builder.Services.AddScoped<USP.Core.Interfaces.Services.Audit.IAuditService, USP.Infrastructure.Services.Audit.AuditService>();

// Redis Distributed Cache
var redisConnectionString = $"{builder.Configuration["Redis:Host"]}:{builder.Configuration["Redis:Port"]},password={builder.Configuration["Redis:Password"]},ssl={builder.Configuration["Redis:EnableSsl"]},abortConnect=false,connectTimeout={builder.Configuration["Redis:ConnectTimeout"]},syncTimeout={builder.Configuration["Redis:SyncTimeout"]}";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "USP:";
});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString!, name: "postgresql", tags: new[] { "db", "ready" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "cache", "ready" });

// Controllers
builder.Services.AddControllers();

// Prometheus Metrics
builder.Services.AddHttpContextAccessor();

// Enable Prometheus metrics collection
Prometheus.Metrics.SuppressDefaultMetrics();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "USP API - Unified Security Platform",
        Version = "v1",
        Description = "Production-ready security platform with authentication, authorization, secrets management, and PAM"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Audit logging (must come after authentication/authorization to capture user identity)
app.UseAuditLogging();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false  // Always returns healthy (liveness check)
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Prometheus metrics endpoint
app.MapMetrics("/metrics")
   .ExcludeFromDescription();

app.MapControllers();

// Default endpoint
app.MapGet("/", () => new
{
    Service = "USP - Unified Security Platform",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTime.UtcNow
})
.WithName("GetServiceInfo")
.WithOpenApi();

app.Run();
