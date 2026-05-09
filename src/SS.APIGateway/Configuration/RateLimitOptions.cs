namespace SS.APIGateway.Configuration;

public sealed class RateLimitOptions
{
    public PolicyOptions GlobalPolicy { get; set; } = new();
    public PolicyOptions BruteForcePolicy { get; set; } = new();

    public sealed class PolicyOptions
    {
        public int PermitLimit { get; set; }
        public string Window { get; set; } = "00:01:00";
        public int QueueLimit { get; set; }
    }
}
