using FoodLoop.Application.Donations;
using Microsoft.Extensions.Options;

namespace FoodLoop.Web.Services;

public sealed class DonationExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DonationExpirySchedulerOptions> options,
    ILogger<DonationExpiryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
        {
            logger.LogInformation("Donation expiry scheduler is disabled.");
            return;
        }

        var interval = configuration.GetInterval();
        logger.LogInformation(
            "Donation expiry scheduler started with an interval of {IntervalSeconds} seconds.",
            interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expiry = scope.ServiceProvider.GetRequiredService<IDonationExpiryService>();
                var result = await expiry.ExpireDueAsync(stoppingToken);

                if (result.Succeeded)
                {
                    if (result.ExpiredCount > 0)
                    {
                        logger.LogInformation(
                            "Donation expiry run completed. Expired {ExpiredCount} donation(s).",
                            result.ExpiredCount);
                    }
                }
                else
                {
                    logger.LogWarning(
                        "Donation expiry run ended because persisted donation state changed concurrently. A later run will re-evaluate current state.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Do not log exception messages/data here: database-provider exceptions can contain
                // environment details. The exception type is enough for operational triage.
                logger.LogError(
                    "Donation expiry run failed with {ExceptionType}. The scheduler will continue on the next interval.",
                    ex.GetType().Name);
            }
        }
    }
}
