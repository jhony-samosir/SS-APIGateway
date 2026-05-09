namespace SS.APIGateway.Extensions;

public static class CorsSecurityExtensions
{
    public static IServiceCollection AddGatewayCorsAndSecurity(
        this IServiceCollection services, IConfiguration config)
    {
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("GatewayPolicy", builder =>
            {
                builder.WithOrigins(allowedOrigins)
                       .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                       .WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With", "X-Correlation-Id")
                       .AllowCredentials();
            });
        });

        return services;
    }
}
