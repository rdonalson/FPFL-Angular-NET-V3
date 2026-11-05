using System.Threading.RateLimiting;

using Fpfl.Api.Controllers;
using Fpfl.Api.Exceptions;
using Fpfl.Api.Health;
using Fpfl.Api.Mappers;
using Fpfl.Api.Responses;
using Fpfl.Api.Security;
using Fpfl.Auth;
using Fpfl.Common;
using Fpfl.Display;
using Fpfl.Items;
using Fpfl.Items.Infrastructure.Health;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
// Secrets live in user-secrets (dev) or environment variables (prod), never in appsettings.json:
//   dotnet user-secrets set "ConnectionStrings:Fpfl" "Host=localhost;Port=5432;Database=FPFL-V2;Username=postgres;Password=..." --project src/Fpfl.Api
//   dotnet user-secrets set "Auth:SigningKey" "<at least 32 random characters>" --project src/Fpfl.Api
var connectionString = builder.Configuration.GetConnectionString("Fpfl")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:Fpfl' is not configured. " +
        "Set it with 'dotnet user-secrets set' or the ConnectionStrings__Fpfl environment variable.");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// ---------------------------------------------------------------------------
// Bounded contexts
// ---------------------------------------------------------------------------
builder.Services.AddFpflCommon();
builder.Services.AddFpflItems(connectionString);
builder.Services.AddFpflDisplay();
builder.Services.AddFpflAuth(builder.Configuration, connectionString);

// 401/403 from JWT bearer auth use the standard ApiResponse envelope.
builder.Services.PostConfigure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options => options.Events = AuthEnvelopeEvents.Create());

// ---------------------------------------------------------------------------
// API layer
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IApiResponseFactory, ApiResponseFactory>();
builder.Services.AddSingleton<ItemTypeMapper>();
builder.Services.AddSingleton<AuthMapper>();
builder.Services.AddSingleton<TimePeriodMapper>();
builder.Services.AddSingleton<ItemMapper>();
builder.Services.AddSingleton<DisplayMapper>();
builder.Services.AddSingleton<DebtMapper>();
builder.Services.AddSingleton<AccountMapper>();

builder.Services.AddControllers();

// Model-binding errors (bad JSON, wrong types, failed [Required] etc.) use the same envelope.
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join("; ", context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
                string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? $"Invalid value for '{entry.Key}'"
                    : error.ErrorMessage)));

        return new BadRequestObjectResult(new ApiResponse<object?>(StatusCodes.Status400BadRequest, message));
    });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info ??= new();
    document.Info.Title = "FPFL API";
    document.Info.Description = "API documentation for FPFL backend services";
    document.Info.Version = "1.0.0";
    return Task.CompletedTask;
}));

// /health lists the same components as API-JDK's actuator: db, diskSpace and ping.
builder.Services.AddHealthChecks()
    .AddCheck<ItemsDatabaseHealthCheck>("db")
    .AddCheck("diskSpace", new DiskSpaceHealthCheck())
    .AddCheck("ping", () => HealthCheckResult.Healthy("API is answering"));

// Anonymous write endpoints (POST /client-logs) are limited per client IP; 429 uses the envelope.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) => new ValueTask(
        context.HttpContext.Response.WriteAsJsonAsync(
            new ApiResponse<object?>(StatusCodes.Status429TooManyRequests, "Too many requests; try again shortly"),
            cancellationToken));
    options.AddPolicy(ClientLogsController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
var app = builder.Build();
var startedAt = DateTimeOffset.UtcNow;

app.UseExceptionHandler();

// HTTPS redirection only outside Development: browsers refuse to follow a redirect on a CORS
// preflight, so redirecting UI-NG's http://localhost:5132 calls would break login.
// In production TLS is terminated by the ingress anyway.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Landing page: wwwroot/index.html at "/"
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI document + Swagger UI (linked from the landing page)
app.MapOpenApi();                       // /openapi/v1.json
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "FPFL API v1");
    options.RoutePrefix = "swagger-ui"; // /swagger-ui
    options.DocumentTitle = "FPFL API";
});

app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

// Version, build and runtime (Actuator-style /info), read by the UI's status page.
app.MapGet("/info", (IHostEnvironment env, TimeProvider clock) =>
        AppInfo.Create(typeof(Program).Assembly, env.EnvironmentName, startedAt, clock.GetUtcNow()))
    .AllowAnonymous()
    .WithTags("Health");

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
