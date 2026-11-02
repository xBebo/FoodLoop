namespace FoodLoop.Web.Services;

public sealed class AuthRateLimitOptions
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}
