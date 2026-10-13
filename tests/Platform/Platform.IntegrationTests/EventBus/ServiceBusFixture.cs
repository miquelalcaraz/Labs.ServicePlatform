using System.Text;

using Testcontainers.ServiceBus;

namespace Platform.IntegrationTests.EventBus;

public sealed class ServiceBusFixture : IAsyncLifetime
{
    public const string TopicName = "miqa-event-bus";
    public const string MainSubscription = "it-main";
    public const string PeekSubscription = "it-peek";

    private ServiceBusContainer _container = null!;

    public ServiceBusClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configBytes = Encoding.UTF8.GetBytes(BuildEmulatorConfig());

        _container = new ServiceBusBuilder()
            .WithAcceptLicenseAgreement(true)
            .WithResourceMapping(configBytes, "/ServiceBus_Emulator/ConfigFiles/Config.json")
            .Build();

        await _container.StartAsync();

        Client = new ServiceBusClient(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Client.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Resets subscription rules to the $Default (TrueFilter) state, as if freshly created.
    /// </summary>
    public async Task ResetSubscriptionRulesAsync(string subscriptionName)
    {
        await using var ruleManager = Client.CreateRuleManager(TopicName, subscriptionName);

        await foreach (var rule in ruleManager.GetRulesAsync())
        {
            await ruleManager.DeleteRuleAsync(rule.Name);
        }

        await ruleManager.CreateRuleAsync(new CreateRuleOptions
        {
            Name = RuleProperties.DefaultRuleName,
            Filter = new TrueRuleFilter()
        });
    }

    /// <summary>
    /// Drains all pending messages from a subscription to provide a clean slate for tests.
    /// </summary>
    public async Task DrainSubscriptionAsync(string subscriptionName)
    {
        await using var receiver = Client.CreateReceiver(TopicName, subscriptionName);

        ServiceBusReceivedMessage? message;
        do
        {
            message = await receiver.ReceiveMessageAsync(maxWaitTime: TimeSpan.FromSeconds(1));
            if (message is not null)
                await receiver.CompleteMessageAsync(message);
        }
        while (message is not null);
    }

    /// <summary>
    /// Creates a <see cref="ServiceBusEventBus"/> wired to the emulator client for use in tests.
    /// </summary>
    public ServiceBusEventBus CreateEventBus(
        IServiceProvider serviceProvider,
        EventBusSubscriptionInfo subscriptionInfo,
        string subscriptionName = MainSubscription)
    {
        var logger = LoggerFactory
            .Create(b => b.AddConsole())
            .CreateLogger<ServiceBusEventBus>();

        return new ServiceBusEventBus(
            logger,
            serviceProvider,
            Client,
            Options.Create(subscriptionInfo),
            Options.Create(new EventBusOptions { SubscriptionClientName = subscriptionName }));
    }

    private static string BuildEmulatorConfig() =>
        """
        {
          "UserConfig": {
            "Namespaces": [
              {
                "Name": "sbemulatorns",
                "Topics": [
                  {
                    "Name": "miqa-event-bus",
                    "Properties": { "MaxSizeInMegabytes": 1024 },
                    "Subscriptions": [
                      {
                        "Name": "it-main",
                        "Properties": { "LockDuration": "PT1M", "MaxDeliveryCount": 10 }
                      },
                      {
                        "Name": "it-peek",
                        "Properties": { "LockDuration": "PT1M", "MaxDeliveryCount": 10 }
                      }
                    ]
                  }
                ]
              }
            ],
            "Logging": { "Type": "File" }
          }
        }
        """;
}
