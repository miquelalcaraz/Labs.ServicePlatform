using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Platform.TestWebApi.Controllers;
using Platform.TestWebApi.Infrastructure;

using Miqa.Platform.Messaging.Models;

namespace Platform.FunctionalTests.Tests;

[Collection("BuildingBlocks")]
public sealed class DomainEventsTests
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public DomainEventsTests(BuildingBlocksFixture fixture)
    {
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient();
    }

    [Fact]
    public async Task CreateEntity_ShouldDispatchDomainEvent_AndGenerateIntegrationEvent()
    {
        var requestId = Guid.NewGuid();
        var entityName = $"DomainEventTest_{requestId:N}";
        var request = new CreateTestEntityRequest(entityName, 999);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);

        var resultJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.GetProperty("isSuccess").GetBoolean());

        using var scope = _webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var entity = await dbContext.TestEntities
            .FirstOrDefaultAsync(e => e.Name == entityName, TestContext.Current.CancellationToken);

        Assert.NotNull(entity);
        Assert.Equal(entityName, entity.Name);
        Assert.Equal(999, entity.Value);

        var outboxMessage = await dbContext.Set<OutboxMessage>()
            .Where(m => m.TypeName.Contains("TestEntityCreatedIntegrationEvent"))
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(outboxMessage);
    }
}
