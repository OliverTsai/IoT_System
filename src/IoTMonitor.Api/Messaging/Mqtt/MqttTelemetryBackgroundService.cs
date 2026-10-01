using System.Buffers;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace IoTMonitor.Api.Messaging.Mqtt;

public sealed class MqttTelemetryBackgroundService(
    IOptions<MqttOptions> options,
    MqttTelemetryProcessor processor,
    ILogger<MqttTelemetryBackgroundService> logger) : BackgroundService
{
    private readonly MqttOptions _options = options.Value;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("MQTT telemetry ingestion is disabled.");
            return;
        }

        _stoppingToken = stoppingToken;
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += HandleMessageAsync;
        client.DisconnectedAsync += args =>
        {
            if (!stoppingToken.IsCancellationRequested && args.ClientWasConnected)
            {
                logger.LogWarning(
                    "MQTT client disconnected: {Reason}. Reconnection will be attempted.",
                    args.Reason);
            }

            return Task.CompletedTask;
        };

        var clientOptions = BuildClientOptions();
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter
                .WithTopic(_options.TopicFilter)
                .WithAtLeastOnceQoS())
            .Build();
        var reconnectDelay = TimeSpan.FromSeconds(_options.ReconnectDelaySeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!client.IsConnected)
                {
                    try
                    {
                        var connectResult = await client.ConnectAsync(clientOptions, stoppingToken);
                        if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
                        {
                            throw new InvalidOperationException(
                                $"MQTT connection was rejected with result '{connectResult.ResultCode}'.");
                        }

                        await client.SubscribeAsync(subscribeOptions, stoppingToken);
                        logger.LogInformation(
                            "MQTT client connected to {Host}:{Port} and subscribed to {TopicFilter}",
                            _options.Host,
                            _options.Port,
                            _options.TopicFilter);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        if (client.IsConnected)
                        {
                            try
                            {
                                var disconnectOptions =
                                    new MqttClientDisconnectOptionsBuilder().Build();
                                await client.DisconnectAsync(
                                    disconnectOptions,
                                    CancellationToken.None);
                            }
                            catch (Exception disconnectException)
                            {
                                logger.LogDebug(
                                    disconnectException,
                                    "MQTT client could not reset after a connection or subscription failure");
                            }
                        }

                        logger.LogWarning(
                            "MQTT connection failed: {Error}. Retrying in {ReconnectDelaySeconds} seconds.",
                            exception.Message,
                            _options.ReconnectDelaySeconds);
                        logger.LogDebug(exception, "MQTT connection failure details");
                    }
                }

                await Task.Delay(reconnectDelay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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
                    logger.LogDebug(exception, "MQTT client could not disconnect cleanly during shutdown");
                }
            }
        }
    }

    private MqttClientOptions BuildClientOptions()
    {
        return new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCredentials(_options.Username, _options.Password)
            .WithCleanSession(false)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
            .WithTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        args.AutoAcknowledge = false;
        var payload = args.ApplicationMessage.Payload.ToArray();

        try
        {
            await processor.ProcessAsync(
                args.ApplicationMessage.Topic,
                payload,
                _stoppingToken);

            if (args.ApplicationMessage.QualityOfServiceLevel !=
                MqttQualityOfServiceLevel.AtMostOnce)
            {
                await args.AcknowledgeAsync(_stoppingToken);
            }
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            args.ProcessingFailed = true;
        }
        catch (Exception exception)
        {
            args.ProcessingFailed = true;
            logger.LogError(
                exception,
                "Failed to process MQTT telemetry on topic {Topic}; the message was not acknowledged.",
                args.ApplicationMessage.Topic);
        }
    }
}
