namespace FoodLoop.Web.Services;

public sealed class DonationExpirySchedulerOptions
{
    public const string SectionName = "DonationExpiryScheduler";

    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;

    public TimeSpan GetInterval()
        => TimeSpan.FromSeconds(Math.Clamp(IntervalSeconds, 1, 86_400));
}
