using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Qlt.Platform.EventBus.Options;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Meters;

namespace Qlt.Platform.EventBus;

public sealed class ServiceBusEventBus(
    ILogger<ServiceBusEventBus> logger,
    IServiceProvider serviceProvider,
    ServiceBusClient client,
    IOptions<EventBusSubscriptionInfo> subscriptionOptions,
    IOptions<EventBusOptions> options
    ) : IEventBus, IAsyncDisposable, IHostedService
{
    internal const string TopicName = "qlt-event-bus";

    private readonly EventBusSubscriptionInfo _subscriptionInfo = subscriptionOptions.Value;
    private Dictionary<string, Type> _normalizedEventTypes = null!;

    private ServiceBusSender _sender = null!;
    private ServiceBusProcessor? _processor;

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async ValueTask DisposeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await StopAsync(cts.Token);
    }

    public async Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        await _started.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        var routingKey = GetEventName(@event);

        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: $"Publish {routingKey}",
            kind: ActivityKind.Producer);

        activity?.SetTag("messaging.system", "azureservicebus");
        activity?.SetTag("messaging.destination.name", TopicName);
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("event.type", routingKey);
        activity?.SetTag("event.id", @event.Id);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Publishing event to ServiceBus: {EventId}", @event.Id);
        }

        var body = SerializeMessage(@event);

        var message = new ServiceBusMessage
        {
            MessageId = @event.Id.ToString(),
            Body = new BinaryData(body),
            Subject = routingKey,
        };

        if (activity != null)
        {
            message.ApplicationProperties["traceparent"] = activity.Id;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                message.ApplicationProperties["tracestate"] = activity.TraceStateString;
            }
        }

        try
        {
            await _sender.SendMessageAsync(message, cancellationToken);

            ApplicationDiagnostics.IntegrationEventsPublished.Add(1,
                new KeyValuePair<string, object?>("event_type", routingKey),
                new KeyValuePair<string, object?>("success", "true"));

            activity?.SetTag("messaging.success", true);

            logger.LogInformation("Successfully published event {EventType} with ID {EventId}", routingKey, @event.Id);
        }
        catch (Exception ex)
        {
            ApplicationDiagnostics.IntegrationEventsPublished.Add(1,
                new KeyValuePair<string, object?>("event_type", routingKey),
                new KeyValuePair<string, object?>("success", "false"));

            activity?.SetTag("messaging.success", false);
            activity?.SetTag("error.type", ex.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            logger.LogError(ex, "Failed to publish event to ServiceBus: {EventId}", @event.Id);
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting ServiceBus processing.");

        try
        {
            if (string.IsNullOrEmpty(options.Value.SubscriptionClientName))
            {
                throw new InvalidOperationException($"EventBus {nameof(EventBusOptions.SubscriptionClientName)} must be set.");
            }

            NormalizeEventTypeNames();

            _sender = client.CreateSender(queueOrTopicName: TopicName);
            _processor = client.CreateProcessor(topicName: TopicName, options.Value.SubscriptionClientName,
                new ServiceBusProcessorOptions { AutoCompleteMessages = false });

            await ConfigureSubscriptionRulesAsync();

            _processor.ProcessMessageAsync += OnMessageReceived;
            _processor.ProcessErrorAsync += OnProcessError;

            await _processor.StartProcessingAsync(cancellationToken);

            _started.TrySetResult();

            logger.LogInformation("ServiceBus started processing successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ServiceBus failed to start processing.");
            _started.TrySetException(ex);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _started.TrySetCanceled();

        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        if (_sender is not null)
        {
            await _sender.DisposeAsync();
        }
    }

    private void NormalizeEventTypeNames()
    {
        _normalizedEventTypes = new Dictionary<string, Type>(_subscriptionInfo.EventTypes.Count);
        foreach (var (key, value) in _subscriptionInfo.EventTypes)
        {
            var normalizedKey = key.Contains("IntegrationEvent")
                ? key.Replace("IntegrationEvent", "")
                : key;
            _normalizedEventTypes[normalizedKey] = value;
        }
    }

    private async Task ConfigureSubscriptionRulesAsync()
    {
        await using var ruleManager = client.CreateRuleManager(TopicName, options.Value.SubscriptionClientName);

        var existingRules = new HashSet<string>();
        await foreach (var rule in ruleManager.GetRulesAsync())
        {
            existingRules.Add(rule.Name);
        }

        if (existingRules.Contains(RuleProperties.DefaultRuleName))
        {
            try
            {
                await ruleManager.DeleteRuleAsync(RuleProperties.DefaultRuleName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound) { }
        }

        foreach (var (eventName, _) in _normalizedEventTypes)
        {
            if (!existingRules.Contains(eventName))
            {
                try
                {
                    await ruleManager.CreateRuleAsync(new CreateRuleOptions
                    {
                        Filter = new CorrelationRuleFilter { Subject = eventName },
                        Name = eventName
                    });
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    logger.LogDebug("Rule for '{EventName}' already exists.", eventName);
                }
            }
        }
    }

    private Task OnProcessError(ProcessErrorEventArgs error)
    {
        logger.LogError(error.Exception, "Error with ServiceBus processor. {ErrorSource}", error.ErrorSource);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(ProcessMessageEventArgs eventArgs)
    {
        var eventName = eventArgs.Message.Subject;
        var message = eventArgs.Message.Body.ToString();

        try
        {
            var processed = await ProcessEvent(eventName, message, eventArgs.Message);

            if (processed)
            {
                await eventArgs.CompleteMessageAsync(eventArgs.Message);
            }
            else
            {
                await eventArgs.DeadLetterMessageAsync(eventArgs.Message,
                    deadLetterReason: "UnknownEventType",
                    deadLetterErrorDescription: $"No handler registered for event '{eventName}'");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message {MessageId} for event {EventName}",
                eventArgs.Message.MessageId, eventName);

            try
            {
                await eventArgs.AbandonMessageAsync(eventArgs.Message);
            }
            catch (Exception abandonEx)
            {
                logger.LogError(abandonEx, "Failed to abandon message {MessageId}",
                    eventArgs.Message.MessageId);
            }
        }
    }

    private async Task<bool> ProcessEvent(string eventName, string message, ServiceBusReceivedMessage? receivedMessage = null)
    {
        ActivityContext parentContext = default;
        if (receivedMessage?.ApplicationProperties != null)
        {
            if (receivedMessage.ApplicationProperties.TryGetValue("traceparent", out var traceparentObj) &&
                traceparentObj is string traceparent)
            {
                ActivityContext.TryParse(traceparent, null, out parentContext);
            }
        }

        var activityName = $"Process {eventName}";
        var activity = parentContext != default
            ? ApplicationDiagnostics.ActivitySource.StartActivity(activityName, ActivityKind.Consumer, parentContext)
            : ApplicationDiagnostics.ActivitySource.StartActivity(activityName, ActivityKind.Consumer);

        using (activity)
        {
            activity?.SetTag("messaging.system", "azureservicebus");
            activity?.SetTag("messaging.source.name", TopicName);
            activity?.SetTag("messaging.operation.name", "process");
            activity?.SetTag("event.type", eventName);

            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Processing ServiceBus event: {EventName}", eventName);
            }

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();

                if (!_normalizedEventTypes.TryGetValue(eventName, out var eventType))
                {
                    logger.LogWarning("Unable to resolve event type for event name {EventName}", eventName);
                    activity?.SetTag("event.resolved", false);
                    return false;
                }

                activity?.SetTag("event.resolved", true);

                var integrationEvent = DeserializeMessage(message, eventType);
                activity?.SetTag("event.id", integrationEvent?.Id);

                var handlerCount = 0;
                var handlerInterfaceType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                var handlers = scope.ServiceProvider.GetServices(handlerInterfaceType);

                foreach (var handler in handlers)
                {
                    handlerCount++;
                    await ((IIntegrationEventHandler)handler).Handle(integrationEvent);
                }

                activity?.SetTag("event.handler_count", handlerCount);

                ApplicationDiagnostics.IntegrationEventsReceived.Add(1,
                    new KeyValuePair<string, object?>("event_type", eventName),
                    new KeyValuePair<string, object?>("success", "true"));

                activity?.SetTag("messaging.success", true);

                logger.LogInformation("Successfully processed event {EventType} with {HandlerCount} handler(s)", eventName, handlerCount);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing event {EventType}", eventName);

                ApplicationDiagnostics.IntegrationEventsReceived.Add(1,
                    new KeyValuePair<string, object?>("event_type", eventName),
                    new KeyValuePair<string, object?>("success", "false"));

                activity?.SetTag("messaging.success", false);
                activity?.SetTag("error.type", ex.GetType().FullName);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                throw;
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed .NET apps, ensures the JsonSerializer doesn't use Reflection.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
    private IntegrationEvent DeserializeMessage(string message, Type eventType)
    {
        return JsonSerializer.Deserialize(message, eventType, _subscriptionInfo.JsonSerializerOptions) as IntegrationEvent;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed .NET apps, ensures the JsonSerializer doesn't use Reflection.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
    private byte[] SerializeMessage(IntegrationEvent @event)
    {
        return JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _subscriptionInfo.JsonSerializerOptions);
    }

    private string GetEventName(IntegrationEvent @event)
    {
        return @event.GetType().Name.Replace("IntegrationEvent", "");
    }
}
