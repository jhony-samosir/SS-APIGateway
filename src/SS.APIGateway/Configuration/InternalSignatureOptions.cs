namespace SS.APIGateway.Configuration;

public sealed class InternalSignatureOptions
{
    public string HeaderName { get; set; } = "X-Internal-Signature";
    public string SecretKeyEnvVar { get; set; } = "GATEWAY_HMAC_SECRET";
}
