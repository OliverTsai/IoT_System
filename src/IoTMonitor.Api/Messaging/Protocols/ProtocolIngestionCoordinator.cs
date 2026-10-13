namespace IoTMonitor.Api.Messaging.Protocols;

public sealed class ProtocolIngestionCoordinator(
    DeviceProtocolRegistry registry,
    ILogger<ProtocolIngestionCoordinator> logger) : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabledAdapters = registry.Adapters
            .Where(adapter => adapter.IsEnabled)
            .ToArray();

        if (enabledAdapters.Length == 0)
        {
            logger.LogInformation("All device protocol ingestion adapters are disabled.");
            return Task.CompletedTask;
        }

        return Task.WhenAll(enabledAdapters.Select(
            adapter => SuperviseAsync(adapter, stoppingToken)));
    }

    private async Task SuperviseAsync(
        IDeviceTelemetryProtocolAdapter adapter,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation(
                    "Starting {ProtocolKey} device protocol ingestion adapter.",
                    adapter.Descriptor.Key);
                await adapter.RunAsync(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                logger.LogWarning(
                    "The {ProtocolKey} device protocol ingestion adapter stopped unexpectedly; " +
                    "it will be restarted.",
                    adapter.Descriptor.Key);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "The {ProtocolKey} device protocol ingestion adapter failed; " +
                    "other adapters will continue and this adapter will be restarted.",
                    adapter.Descriptor.Key);
            }

            try
            {
                await Task.Delay(RestartDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
