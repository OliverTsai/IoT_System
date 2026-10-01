using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

namespace IoTMonitor.DeviceSimulator;

internal static class Program
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main()
    {
        SimulatorOptions options;

        try
        {
            options = SimulatorOptions.FromEnvironment();
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"Configuration error: {exception.Message}");
            return 1;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        client.DisconnectedAsync += args =>
        {
            if (!cancellation.IsCancellationRequested && args.ClientWasConnected)
            {
                Console.Error.WriteLine(
                    $"MQTT disconnected ({args.Reason}); the simulator will reconnect.");
            }

            return Task.CompletedTask;
        };

        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(options.MqttHost, options.MqttPort)
            .WithClientId(options.MqttClientId)
            .WithCredentials(options.MqttUsername, options.MqttPassword)
            .WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
            .WithTimeout(TimeSpan.FromSeconds(10))
            .Build();

        Console.WriteLine(
            $"Simulating {options.DeviceCount} device(s) through " +
            $"{options.MqttHost}:{options.MqttPort} every {options.IntervalSeconds} second(s).");

        var completedBatches = 0;

        try
        {
            while (!cancellation.IsCancellationRequested &&
                   (options.MessagesPerDevice == 0 ||
                    completedBatches < options.MessagesPerDevice))
            {
                if (!await EnsureConnectedAsync(
                        client,
                        clientOptions,
                        options,
                        cancellation.Token))
                {
                    break;
                }

                try
                {
                    for (var index = 1; index <= options.DeviceCount; index++)
                    {
                        var externalDeviceId = options.GetExternalDeviceId(index);
                        var payload = new SimulatorTelemetryPayload(
                            Guid.NewGuid(),
                            decimal.Round(18m + (decimal)(Random.Shared.NextDouble() * 17), 2),
                            decimal.Round(35m + (decimal)(Random.Shared.NextDouble() * 40), 2),
                            DateTimeOffset.UtcNow);
                        var json = JsonSerializer.Serialize(payload, SerializerOptions);
                        var message = new MqttApplicationMessageBuilder()
                            .WithTopic($"devices/{externalDeviceId}/telemetry")
                            .WithPayload(json)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .Build();

                        var result = await client.PublishAsync(message, cancellation.Token);
                        if (!result.IsSuccess)
                        {
                            throw new InvalidOperationException(
                                $"MQTT publish failed with reason '{result.ReasonCode}'.");
                        }

                        Console.WriteLine(
                            $"Published {payload.MessageId} for {externalDeviceId}: " +
                            $"{payload.TemperatureCelsius}°C, {payload.HumidityPercent}% RH");
                    }

                    completedBatches++;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"Publish failed: {exception.Message} Reconnection will be attempted.");
                    await Task.Delay(
                        TimeSpan.FromSeconds(options.ReconnectDelaySeconds),
                        cancellation.Token);
                    continue;
                }

                if (options.MessagesPerDevice == 0 ||
                    completedBatches < options.MessagesPerDevice)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(options.IntervalSeconds),
                        cancellation.Token);
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    var disconnectOptions = new MqttClientDisconnectOptionsBuilder().Build();
                    await client.DisconnectAsync(disconnectOptions, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"MQTT client could not disconnect cleanly: {exception.Message}");
                }
            }
        }

        return 0;
    }

    private static async Task<bool> EnsureConnectedAsync(
        IMqttClient client,
        MqttClientOptions clientOptions,
        SimulatorOptions options,
        CancellationToken cancellationToken)
    {
        while (!client.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ConnectAsync(clientOptions, cancellationToken);
                if (result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    throw new InvalidOperationException(
                        $"MQTT connection was rejected with result '{result.ResultCode}'.");
                }

                Console.WriteLine("MQTT simulator connected.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"MQTT connection failed: {exception.Message} " +
                    $"Retrying in {options.ReconnectDelaySeconds} second(s).");
                await Task.Delay(
                    TimeSpan.FromSeconds(options.ReconnectDelaySeconds),
                    cancellationToken);
            }
        }

        return client.IsConnected;
    }
}
