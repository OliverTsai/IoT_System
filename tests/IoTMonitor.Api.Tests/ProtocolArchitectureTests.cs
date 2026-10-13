using System.Text.Json;
using IoTMonitor.Api.Messaging.Protocols;
using Microsoft.Extensions.Logging.Abstractions;

namespace IoTMonitor.Api.Tests;

public sealed class ProtocolArchitectureTests
{
    [Fact]
    public void Registry_DiscoversAdaptersWithoutProtocolSpecificBranching()
    {
        var registry = new DeviceProtocolRegistry([
            new StubProtocolAdapter("zeta"),
            new StubProtocolAdapter("alpha")
        ]);

        Assert.Equal(["alpha", "zeta"], registry.Descriptors.Select(item => item.Key));
        Assert.True(registry.TryGetAdapter("zeta", out var adapter));
        Assert.Equal("zeta", adapter!.Descriptor.Key);
        Assert.False(registry.TryGetAdapter("missing", out _));
    }

    [Fact]
    public void Registry_RejectsDuplicateProtocolKeys()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DeviceProtocolRegistry([
                new StubProtocolAdapter("duplicate"),
                new StubProtocolAdapter("duplicate")
            ]));

        Assert.Contains("registered more than once", exception.Message);
    }

    [Fact]
    public void Registry_RejectsOversizedSettingsBeforeCallingAdapter()
    {
        var adapter = new StubProtocolAdapter("bounded");
        var registry = new DeviceProtocolRegistry([adapter]);
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(new { value = new string('x', 17 * 1024) }));

        var result = registry.ValidateSettings(adapter, document.RootElement);

        Assert.False(result.IsValid);
        Assert.Contains("cannot exceed", result.Errors["ProtocolSettings"].Single());
    }

    [Fact]
    public async Task Coordinator_FailedAdapterDoesNotStopOtherAdapters()
    {
        var failed = new StubProtocolAdapter(
            "failed",
            _ => throw new InvalidOperationException("Expected test failure."));
        var healthy = new StubProtocolAdapter("healthy", async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        var registry = new DeviceProtocolRegistry([failed, healthy]);
        var coordinator = new ProtocolIngestionCoordinator(
            registry,
            NullLogger<ProtocolIngestionCoordinator>.Instance);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);
        await healthy.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, failed.RunCount);
        Assert.Equal(1, healthy.RunCount);

        await coordinator.StopAsync(CancellationToken.None);
    }

    private sealed class StubProtocolAdapter(
        string key,
        Func<CancellationToken, Task>? run = null) : IDeviceTelemetryProtocolAdapter
    {
        public DeviceProtocolDescriptor Descriptor { get; } = new(key, key, []);

        public bool IsEnabled => true;

        public int RunCount { get; private set; }

        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public DeviceProtocolSettingsValidationResult ValidateSettings(JsonElement? settings)
        {
            return DeviceProtocolSettingsValidationResult.Valid("{}");
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            RunCount++;
            Started.TrySetResult(true);
            return run?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }
    }
}
