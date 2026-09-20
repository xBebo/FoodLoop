using FoodLoop.Application.Donations;
using FoodLoop.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task Hosted_loop_does_not_overlap_and_shutdown_cancels_and_disposes_active_scope()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var services = new ServiceCollection();
        services.AddScoped<IDonationExpiryService>(_ => new CallbackExpiry(async ct =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return DonationExpiryResult.Success(0);
        }, () => disposed.TrySetResult()));
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();
        using var scheduler = new DonationExpiryBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DonationExpirySchedulerOptions { IntervalSeconds = 1 }), logger);
        await scheduler.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(1200); // Another interval passes while the first run remains blocked.
        Assert.Equal(1, Volatile.Read(ref calls));
        await scheduler.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(logger.Entries, x => x.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Hosted_loop_recovers_from_failure_with_fresh_scope_and_safe_logs(bool unexpected)
    {
        const string sensitive = "raw-token-or-provider-connection-secret";
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var instances = 0;
        var disposed = 0;
        var services = new ServiceCollection();
        services.AddScoped<IDonationExpiryService>(_ =>
        {
            var instance = Interlocked.Increment(ref instances);
            return new CallbackExpiry(ct =>
            {
                if (instance == 1)
                {
                    if (unexpected) throw new InvalidOperationException(sensitive);
                    return Task.FromResult(DonationExpiryResult.Conflict());
                }
                recovered.TrySetResult();
                return Task.FromResult(DonationExpiryResult.Success(0));
            }, () => Interlocked.Increment(ref disposed));
        });
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();
        using var scheduler = new DonationExpiryBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DonationExpirySchedulerOptions { IntervalSeconds = 1 }), logger);
        await scheduler.StartAsync(default);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await scheduler.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(instances >= 2);
        Assert.Equal(instances, disposed);
        Assert.Contains(logger.Entries, x => x.Level == (unexpected ? LogLevel.Error : LogLevel.Warning));
        Assert.All(logger.Entries, x => { Assert.Null(x.Exception); Assert.DoesNotContain(sensitive, x.Message); });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Disabled_or_stopped_during_delay_does_not_resolve_expiry(bool enabled)
    {
        var services = new ServiceCollection();
        services.AddScoped<IDonationExpiryService>(_ => throw new InvalidOperationException("Must not resolve"));
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();
        using var scheduler = new DonationExpiryBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DonationExpirySchedulerOptions { Enabled = enabled, IntervalSeconds = 86400 }), logger);
        await scheduler.StartAsync(default);
        await scheduler.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(logger.Entries, x => x.Level >= LogLevel.Warning);
    }

    private sealed class CallbackExpiry(Func<CancellationToken, Task<DonationExpiryResult>> callback, Action onDispose)
        : IDonationExpiryService, IAsyncDisposable
    {
        public Task<DonationExpiryResult> ExpireDueAsync(CancellationToken cancellationToken = default) => callback(cancellationToken);
        public ValueTask DisposeAsync() { onDispose(); return ValueTask.CompletedTask; }
    }

    private sealed class RecordingLogger : ILogger<DonationExpiryBackgroundService>
    {
        public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Enqueue((logLevel, formatter(state, exception), exception));
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
