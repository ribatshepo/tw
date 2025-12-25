using System.Reflection;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using USP.Api.Grpc;
using USP.Api.Middleware;
using Fido2NetLib;
using USP.Core.Models.Configuration;
using USP.Core.Models.Entities;
using USP.Core.Services.ApiKey;
using USP.Core.Services.Audit;
using USP.Core.Services.Authentication;
using USP.Core.Services.Authorization;
using USP.Core.Services.Communication;
using USP.Core.Services.Compliance;
using USP.Core.Services.Cryptography;
using USP.Core.Services.Device;
using USP.Core.Services.Mfa;
using USP.Core.Services.PAM;
using USP.Core.Services.Secrets;
using USP.Core.Services.Session;
using USP.Core.Services.Webhook;
using USP.Core.Validators.Authentication;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.ApiKey;
using USP.Infrastructure.Services.Audit;
using USP.Infrastructure.Services.Authentication;
using USP.Infrastructure.Services.Authorization;
using USP.Infrastructure.Services.Communication;
using USP.Infrastructure.Services.Compliance;
using USP.Infrastructure.Services.Cryptography;
using USP.Infrastructure.Services.Device;
using USP.Infrastructure.Services.Mfa;
using USP.Infrastructure.Services.PAM;
using USP.Infrastructure.Services.Secrets;
using USP.Infrastructure.Services.Session;
using USP.Infrastructure.Services.Webhook;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "USP")
    .CreateLogger();

try
{
    Log.Information("Starting USP (Unified Security Platform) service");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Database
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ASP.NET Core Identity
    builder.Services.AddIdentity<ApplicationUser, Role>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

        // Sign-in settings
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // JWT Authentication
    var jwtAlgorithm = builder.Configuration["Jwt:Algorithm"] ?? "HS256";
    var jwtKey = jwtAlgorithm == "HS256"
        ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is required")))
        : null;

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = jwtKey,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Debug("Token validated for user: {User}", context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    // Email Configuration and Service
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
    builder.Services.AddScoped<IEmailService, EmailService>();

    // SMS Service
    builder.Services.AddScoped<ISmsService, SmsService>();

    // Distributed Cache (Redis) - for WebAuthn challenges and session storage
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        var redisConnection = builder.Configuration.GetSection("Redis:ConnectionString").Value
            ?? throw new InvalidOperationException("Redis connection string is required");
        options.Configuration = redisConnection;
        options.InstanceName = "USP:";
    });

    // HttpClient for external API calls (geolocation, etc.)
    builder.Services.AddHttpClient();

    // WebAuthn/FIDO2 Configuration
    builder.Services.Configure<WebAuthnSettings>(builder.Configuration.GetSection("WebAuthn"));
    builder.Services.AddFido2(options =>
    {
        var webAuthnSettings = builder.Configuration.GetSection("WebAuthn").Get<WebAuthnSettings>()
            ?? throw new InvalidOperationException("WebAuthn configuration is required");

        options.ServerDomain = webAuthnSettings.RelyingPartyId;
        options.ServerName = webAuthnSettings.RelyingPartyName;
        options.Origins = new HashSet<string> { webAuthnSettings.Origin };
        options.TimestampDriftTolerance = webAuthnSettings.TimestampDriftTolerance;
    });

    // Application Services
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IMfaService, MfaService>();
    builder.Services.AddScoped<IGeolocationService, GeolocationService>();
    builder.Services.AddScoped<IDeviceFingerprintService, DeviceFingerprintService>();
    builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
    builder.Services.AddScoped<IApiKeyManagementService, ApiKeyManagementService>();

    // Audit and Compliance Services
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IComplianceEngine, ComplianceEngine>();

    // Webhook Services
    builder.Services.AddScoped<IWebhookService, WebhookService>();

    // PAM Services
    builder.Services.AddScoped<ISafeManagementService, SafeManagementService>();
    builder.Services.AddScoped<ICheckoutService, CheckoutService>();
    builder.Services.AddScoped<IDualControlService, DualControlService>();

    // Advanced Authentication Services
    builder.Services.AddScoped<IWebAuthnService, WebAuthnService>();
    builder.Services.AddScoped<IOAuth2Service, OAuth2Service>();
    builder.Services.AddScoped<IPasswordlessAuthService, PasswordlessAuthService>();
    builder.Services.AddScoped<IRiskAssessmentService, USP.Infrastructure.Services.Risk.RiskAssessmentService>();

    // Authorization Services
    builder.Services.AddScoped<IAbacEngine, AbacEngine>();
    builder.Services.AddScoped<IHclPolicyEvaluator, HclPolicyEvaluator>();
    builder.Services.AddScoped<IAuthorizationFlowService, AuthorizationFlowService>();

    // Cryptography and Secrets Services
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
    builder.Services.AddSingleton<IShamirSecretSharing, ShamirSecretSharing>();
    builder.Services.AddSingleton<ISealManager, SealManager>();
    builder.Services.AddScoped<IKvEngine, KvEngine>();

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
    builder.Services.AddFluentValidationAutoValidation();

    // Controllers
    builder.Services.AddControllers();

    // gRPC
    builder.Services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
        options.MaxSendMessageSize = 4 * 1024 * 1024; // 4 MB
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000" };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "db", "postgresql" })
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "self" });

    // Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "USP API - Unified Security Platform",
            Version = "v1",
            Description = "Enterprise-grade security platform providing authentication, authorization, secrets management, and PAM",
            Contact = new OpenApiContact
            {
                Name = "GBMM Platform Team",
                Email = "platform@gbmm.local"
            }
        });

        // JWT Bearer authentication
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // XML comments
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "USP API v1");
            options.RoutePrefix = "swagger";
        });
    }

    // Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // Audit logging middleware
    app.UseMiddleware<AuditLoggingMiddleware>();

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        await next();
    });

    app.UseHttpsRedirection();

    // Prometheus metrics
    app.UseMetricServer(9090);
    app.UseHttpMetrics();

    app.UseCors();

    // API key authentication middleware (before JWT authentication)
    app.UseApiKeyAuthentication();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // gRPC services
    app.MapGrpcService<GrpcAuthenticationService>();
    app.MapGrpcService<GrpcAuthorizationService>();
    app.MapGrpcService<GrpcSecretsService>();

    // Health checks
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("self")
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("db")
    });

    // Root endpoint
    app.MapGet("/", () => new
    {
        service = "USP - Unified Security Platform",
        version = "1.0.0",
        status = "running",
        endpoints = new
        {
            swagger = "/swagger",
            health = "/health",
            healthLive = "/health/live",
            healthReady = "/health/ready",
            metrics = "http://localhost:9090/metrics",
            auth = "/api/auth"
        }
    })
    .WithName("GetServiceInfo")
    .WithTags("System")
    .AllowAnonymous();

    // Initialize built-in roles and permissions
    using (var scope = app.Services.CreateScope())
    {
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
        await roleService.InitializeBuiltInRolesAsync();
    }

    Log.Information("USP service started successfully");
    Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("Swagger UI: {SwaggerUrl}", app.Environment.IsDevelopment() ? "https://localhost:8443/swagger" : "disabled");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "USP service terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
