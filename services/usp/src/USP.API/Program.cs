using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using USP.Core.Domain.Entities.Identity;
using USP.Infrastructure.Persistence;
using USP.Shared.Configuration.Options;

var builder = WebApplication.CreateBuilder(args);

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

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString!, name: "postgresql", tags: new[] { "db", "ready" });

// Controllers
builder.Services.AddControllers();

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

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

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
