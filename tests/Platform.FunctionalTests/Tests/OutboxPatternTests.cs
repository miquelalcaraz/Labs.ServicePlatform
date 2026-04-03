using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Platform.TestWebApi.Controllers;
using Platform.TestWebApi.Infrastructure;

using Qlt.Platform.Messaging.Models;

namespace Platform.FunctionalTests.Tests;

[Collection("BuildingBlocks")]
public sealed class OutboxPatternTests
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public OutboxPatternTests(BuildingBlocksFixture fixture)
    {
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient();
    }

    [Fact]
    public async Task CreateEntity_ShouldStoreIntegrationEventInOutbox()
    {
        var requestId = Guid.NewGuid();
        var entityName = $"OutboxTest_{requestId:N}";
        var request = new CreateTestEntityRequest(entityName, 100);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);

        await Task.Delay(500);

        using var scope = _webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var outboxMessages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.TypeName.Contains("TestEntityCreatedIntegrationEvent"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(outboxMessages);
        Assert.Contains(outboxMessages, m => m.TypeName.Contains("TestEntityCreatedIntegrationEvent"));
    }
}
