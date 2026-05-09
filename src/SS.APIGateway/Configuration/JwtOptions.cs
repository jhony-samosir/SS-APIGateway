namespace SS.APIGateway.Configuration;

public sealed class JwtOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "RS256";
    public string PublicKeyPath { get; set; } = string.Empty;
}
