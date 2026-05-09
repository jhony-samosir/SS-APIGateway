using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SS.APIGateway.Configuration;
using System.Security.Cryptography;

namespace SS.APIGateway.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT Bearer authentication with RS256 validation.
    /// Stateless: no introspection call — validates signature locally using public key.
    /// </summary>
    public static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var jwtOpts = config.GetSection("Jwt").Get<JwtOptions>() 
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        
        // Load RSA public key from PEM file (injected via K8s Secret)
        var rsaPublicKey = LoadRsaPublicKey(jwtOpts.PublicKeyPath);
        var signingKey = new RsaSecurityKey(rsaPublicKey);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = string.IsNullOrWhiteSpace(jwtOpts.Issuer) ? jwtOpts.Authority : jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = ctx =>
                    {
                        // Suppress default response; return clean 401
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync(
                            """{"error":"unauthorized","message":"JWT token is missing or invalid"}""");
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAuthenticatedUser", p => p.RequireAuthenticatedUser());

        return services;
    }

    private static RSA LoadRsaPublicKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Public key path is not configured.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"JWT public key file not found at: {path}");
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(path));
            return rsa;
        }
        catch (Exception ex)
        {
            rsa.Dispose();
            throw new InvalidOperationException($"Failed to load JWT public key from {path}", ex);
        }
    }
}
