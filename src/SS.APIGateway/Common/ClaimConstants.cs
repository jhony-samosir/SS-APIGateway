namespace SS.APIGateway.Common;

/// <summary>
/// Canonical JWT claim names used across the SamStore ecosystem.
/// </summary>
public static class ClaimConstants
{
    public const string UserId = "sub";
    public const string PublicId = "public_id";
    public const string Role = "role";
    public const string Permissions = "permissions";
}
