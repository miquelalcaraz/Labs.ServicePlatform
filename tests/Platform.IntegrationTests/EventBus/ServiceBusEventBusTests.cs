namespace Platform.IntegrationTests.EventBus;

public class ServiceBusEventBusTests : IClassFixture<ServiceBusFixture>, IAsyncLifetime
{
    private readonly ServiceBusFixture _fixture;

    public ServiceBusEventBusTests(ServiceBusFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.WhenAll(
        _fixture.ResetSubscriptionRulesAsync(ServiceBusFixture.MainSubscription),
        _fixture.DrainSubscriptionAsync(ServiceBusFixture.PeekSubscription));

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartAsync_RegistersCorrelationFilterRule_ForEachSubscribedEventType()
    {
        var subscriptionInfo = BuildSubscriptionInfo<TestEvent>();
        var eventBus = _fixture.CreateEventBus(EmptyServiceProvider(), subscriptionInfo);

        await eventBus.StartAsync(CancellationToken.None);
        try
        {
            await using var ruleManager = _fixture.Client.CreateRuleManager(
                ServiceBusFixture.TopicName, ServiceBusFixture.MainSubscription);

            var rules = new List<RuleProperties>();
            await foreach (var rule in ruleManager.GetRulesAsync())
                rules.Add(rule);

            var matchingRule = rules.FirstOrDefault(r => r.Name == nameof(TestEvent));
            matchingRule.Should().NotBeNull("a CorrelationRuleFilter for '{0}' should have been created", nameof(TestEvent));
            matchingRule!.Filter.Should().BeOfType<CorrelationRuleFilter>()
                .Which.Subject.Should().Be(nameof(TestEvent));
        }
        finally
        {
            await eventBus.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_RemovesDefaultRule_FromSubscription()
    {
        var subscriptionInfo = BuildSubscriptionInfo<TestEvent>();
        var eventBus = _fixture.CreateEventBus(EmptyServiceProvider(), subscriptionInfo);

        await eventBus.StartAsync(CancellationToken.None);
        try
        {
            await using var ruleManager = _fixture.Client.CreateRuleManager(
                ServiceBusFixture.TopicName, ServiceBusFixture.MainSubscription);

            var ruleNames = new List<string>();
            await foreach (var rule in ruleManager.GetRulesAsync())
                ruleNames.Add(rule.Name);

            ruleNames.Should().NotContain(RuleProperties.DefaultRuleName);
        }
        finally
        {
            await eventBus.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_StripsIntegrationEventSuffix_FromRuleNames()
    {
        var subscriptionInfo = new EventBusSubscriptionInfo();
        subscriptionInfo.EventTypes[typeof(TestOrderIntegrationEvent).Name] = typeof(TestOrderIntegrationEvent);

        var eventBus = _fixture.CreateEventBus(EmptyServiceProvider(), subscriptionInfo);

        await eventBus.StartAsync(CancellationToken.None);
        try
        {
            await using var ruleManager = _fixture.Client.CreateRuleManager(
                ServiceBusFixture.TopicName, ServiceBusFixture.MainSubscription);

            var ruleNames = new List<string>();
            await foreach (var rule in ruleManager.GetRulesAsync())
                ruleNames.Add(rule.Name);

            ruleNames.Should().Contain("TestOrder");
            ruleNames.Should().NotContain(typeof(TestOrderIntegrationEvent).Name);
        }
        finally
        {
            await eventBus.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PublishAsync_SetsCorrectSubjectAndMessageId_OnServiceBusMessage()
    {
        var @event = new TestEvent { Value = 42 };
        var subscriptionInfo = BuildSubscriptionInfo<TestEvent>();
        var eventBus = _fixture.CreateEventBus(EmptyServiceProvider(), subscriptionInfo);

        await eventBus.StartAsync(CancellationToken.None);
        try
        {
            await eventBus.PublishAsync(@event);

            await using var receiver = _fixture.Client.CreateReceiver(
                ServiceBusFixture.TopicName,
                ServiceBusFixture.PeekSubscription);

            var message = await receiver.ReceiveMessageAsync(maxWaitTime: TimeSpan.FromSeconds(15));

            message.Should().NotBeNull();
            message!.Subject.Should().Be(nameof(TestEvent));
            message.MessageId.Should().Be(@event.Id.ToString());

            await receiver.CompleteMessageAsync(message);
        }
        finally
        {
            await eventBus.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnMessageReceived_WithRegisteredHandler_DispatchesEventToHandler()
    {
        var tcs = new TaskCompletionSource<TestEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        var services = new ServiceCollection();
        services.AddTransient<IIntegrationEventHandler<TestEvent>>(
            _ => new CallbackHandler<TestEvent>(e => tcs.TrySetResult(e)));

        var subscriptionInfo = BuildSubscriptionInfo<TestEvent>();
        var eventBus = _fixture.CreateEventBus(services.BuildServiceProvider(), subscriptionInfo);

        await eventBus.StartAsync(CancellationToken.None);
        try
        {
            var expected = new TestEvent { Value = 99 };
            await eventBus.PublishAsync(expected);

            var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));

            received.Id.Should().Be(expected.Id);
            received.Value.Should().Be(expected.Value);
        }
        finally
        {
            await eventBus.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnMessageReceived_WithNoRegisteredHandler_CompletesMessage_WithoutDeadLettering()
    {
        var subscriptionInfo = BuildSubscriptionInfo<TestEvent>();
        var eventBus = _fixture.CreateEventBus(EmptyServiceProvider(), subscriptionInfo);

        await eventBus.StartAsync(CancellationToken.None);
        try
        {
            await eventBus.PublishAsync(new TestEvent { Value = 1 });

            await Task.Delay(TimeSpan.FromSeconds(5));

            await using var dlqReceiver = _fixture.Client.CreateReceiver(
                ServiceBusFixture.TopicName,
                ServiceBusFixture.MainSubscription,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            var deadLetter = await dlqReceiver.ReceiveMessageAsync(maxWaitTime: TimeSpan.FromSeconds(3));

            deadLetter.Should().BeNull("the message should have been completed by the bus, not dead-lettered");
        }
        finally
        {
            await eventBus.StopAsync(CancellationToken.None);
        }
    }

    private static EventBusSubscriptionInfo BuildSubscriptionInfo<T>() where T : IntegrationEvent
    {
        var info = new EventBusSubscriptionInfo();
        info.EventTypes[typeof(T).Name] = typeof(T);
        return info;
    }

    private static IServiceProvider EmptyServiceProvider() =>
        new ServiceCollection().BuildServiceProvider();

    private record TestEvent : IntegrationEvent
    {
        public int Value { get; init; }
    }

    private record TestOrderIntegrationEvent : IntegrationEvent;

    private sealed class CallbackHandler<T>(Action<T> callback) : IIntegrationEventHandler<T>
        where T : IntegrationEvent
    {
        public Task Handle(T @event)
        {
            callback(@event);
            return Task.CompletedTask;
        }
    }
}
