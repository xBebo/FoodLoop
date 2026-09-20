using FoodLoop.Application.Donations;
using FoodLoop.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoodLoop.Foundation.Tests;

public sealed class DonationExpirySchedulerTests
{
    [Fact]
    public async Task RunOnce_uses_a_fresh_scoped_expiry_service_each_time()
    {
        var probe = new ProbeState();
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddScoped<IDonationExpiryService, ProbeExpiryService>();

        await using var provider = services.BuildServiceProvider();
        var scheduler = new DonationExpiryBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DonationExpirySchedulerOptions { Enabled = true, IntervalSeconds = 60 }),
            NullLogger<DonationExpiryBackgroundService>.Instance);

        await scheduler.RunOnceAsync();
        await scheduler.RunOnceAsync();

        Assert.Equal(2, probe.Calls);
        Assert.Equal(2, probe.InstanceIds.Distinct().Count());
    }

    [Theory]
    [InlineData(-10, 1)]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(100000, 86400)]
    public void Scheduler_interval_is_configurable_and_bounded(int configuredSeconds, int expectedSeconds)
    {
        var options = new DonationExpirySchedulerOptions { IntervalSeconds = configuredSeconds };
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), options.GetInterval());
    }

    private sealed class ProbeState
    {
        public int Calls { get; set; }
        public List<Guid> InstanceIds { get; } = [];
    }

    private sealed class ProbeExpiryService(ProbeState state) : IDonationExpiryService
    {
        private readonly Guid instanceId = Guid.NewGuid();

        public Task<DonationExpiryResult> ExpireDueAsync(CancellationToken cancellationToken = default)
        {
            state.Calls++;
            state.InstanceIds.Add(instanceId);
            return Task.FromResult(DonationExpiryResult.Success(0));
        }
    }
}
