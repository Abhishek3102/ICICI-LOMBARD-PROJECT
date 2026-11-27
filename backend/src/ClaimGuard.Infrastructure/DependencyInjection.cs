using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;
using ClaimGuard.Infrastructure.AI;
using ClaimGuard.Infrastructure.Caching;
using ClaimGuard.Infrastructure.Messaging;
using ClaimGuard.Infrastructure.Persistence;
using ClaimGuard.Infrastructure.Security;
using ClaimGuard.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaimGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("Supabase"))
               .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        // Redis (Upstash). Registered as singleton: ConnectionMultiplexer is thread-safe and
        // meant to be shared for the process lifetime. Degrades gracefully when unreachable.
        services.AddSingleton<ICacheService>(sp =>
            new UpstashRedisService(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Options.RedisOptions>>(),
                sp.GetRequiredService<ILogger<UpstashRedisService>>()));

        // Kafka
        services.AddSingleton<KafkaProducer>();
        services.AddSingleton<IEventBus, KafkaEventBus>();
        services.AddHostedService<KafkaConsumerWorker>();

        // RabbitMQ (CloudAMQP)
        services.AddSingleton<ITaskQueue, RabbitMqPublisher>();
        services.AddHostedService<NotificationWorker>();

        // AI triage
        services.AddScoped<FraudScoringService>();
        services.AddScoped<IAiScoringService, GeminiClaimsAnalyzer>();
        services.AddHttpClient("gemini");
        services.AddHttpClient<SupabaseStorageService>();
        services.AddScoped<ISupabaseStorageService>(sp =>
            sp.GetRequiredService<SupabaseStorageService>());

        // Security
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        // Application services
        services.AddScoped<ClaimService>();
        services.AddScoped<AdjudicationService>();

        return services;
    }
}
