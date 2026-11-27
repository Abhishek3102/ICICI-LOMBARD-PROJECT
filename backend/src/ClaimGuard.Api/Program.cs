using System.Security.Claims;
using System.Text;
using ClaimGuard.Api.Middleware;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Infrastructure;
using ClaimGuard.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ---------- Options ----------
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SupabaseOptions>(builder.Configuration.GetSection(SupabaseOptions.SectionName));

// ---------- Layers ----------
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- Authentication (JWT) ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningSecret)),
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

// ---------- Controllers & Swagger ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- CORS (Next.js frontend) ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------- Middleware pipeline (order matters) ----------
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RedisRateLimitMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Prometheus: scraped at /metrics (p95 latency, throughput, error rates via UseHttpMetrics).
app.UseHttpMetrics();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics("/metrics");

app.Start();
// Explicit startup line: the default "Now listening on" message is hidden by
// the Microsoft.AspNetCore=Warning log level in appsettings.json.
app.Logger.LogInformation("[v0] ClaimGuard API listening on {Urls}", string.Join(", ", app.Urls));
app.WaitForShutdown();

/// <summary>Exposed for integration tests.</summary>
public partial class Program;
