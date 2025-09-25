using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SteelMillShared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSteelMillAuthentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["AzureAd:Authority"];
                options.Audience = configuration["AzureAd:Audience"];
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
                
                // Configure for development
                if (configuration.GetValue<bool>("IsDevelopment"))
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                }
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireOperatorRole", policy =>
                policy.RequireClaim("roles", "SteelMill.Operator"));
            
            options.AddPolicy("RequireManagerRole", policy =>
                policy.RequireClaim("roles", "SteelMill.Manager"));
            
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireClaim("roles", "SteelMill.Admin"));
        });

        return services;
    }

    public static IServiceCollection AddSteelMillHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Service is running"))
            .AddCheck<DatabaseHealthCheck>("database");

        // Cosmos DB health check
        var cosmosConnectionString = configuration["CosmosDb:ConnectionString"];
        if (!string.IsNullOrEmpty(cosmosConnectionString))
        {
            healthChecksBuilder.AddCosmosDb(
                cosmosConnectionString,
                database: configuration["CosmosDb:DatabaseName"] ?? "SteelMillTelemetry",
                name: "cosmosdb",
                failureStatus: HealthStatus.Degraded,
                timeout: TimeSpan.FromSeconds(30));
        }

        // Event Hub health check
        var eventHubConnectionString = configuration["EventHub:ConnectionString"];
        var eventHubName = configuration["EventHub:EventHubName"];
        if (!string.IsNullOrEmpty(eventHubConnectionString) && !string.IsNullOrEmpty(eventHubName))
        {
            healthChecksBuilder.AddAzureEventHubs(
                eventHubConnectionString,
                eventHubName,
                name: "eventhub",
                failureStatus: HealthStatus.Degraded,
                timeout: TimeSpan.FromSeconds(30));
        }

        return services;
    }

    public static IServiceCollection AddSteelMillTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
            options.EnableQuickPulseMetricStream = true;
            options.EnableAdaptiveSampling = true;
            options.EnableHeartbeat = true;
        });

        return services;
    }
}

public class DatabaseHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Add specific database health check logic here
            return Task.FromResult(HealthCheckResult.Healthy("Database is accessible"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database is not accessible", ex));
        }
    }
}