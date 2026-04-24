namespace KJ.Infrastructure.Identity;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "KJ.App";
    public string Audience { get; set; } = "KJ.Client";
    public int ExpirationMinutes { get; set; } = 60;
}

