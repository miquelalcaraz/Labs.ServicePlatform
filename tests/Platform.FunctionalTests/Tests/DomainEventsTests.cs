using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Platform.TestWebApi.Controllers;
using Platform.TestWebApi.Infrastructure;

using Qlt.Platform;
using Qlt.Platform.Messaging.Models;

namespace Platform.FunctionalTests.Tests;

public sealed class DomainEventsTests : IClassFixture<BuildingBlocksFixture>
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
        var request = new CreateTestEntityRequest("DomainEventTest", 999);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);

        var resultJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<Result<int>>(resultJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.True(result!.IsSuccess);

        using var scope = _webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var entity = await dbContext.TestEntities
            .FirstOrDefaultAsync(e => e.Id.GetHashCode() == result.Value, TestContext.Current.CancellationToken);

        Assert.NotNull(entity);
        Assert.Equal("DomainEventTest", entity.Name);
        Assert.Equal(999, entity.Value);

        var outboxMessage = await dbContext.Set<OutboxMessage>()
            .Where(m => m.EventTypeName.Contains("TestEntityCreatedIntegrationEvent"))
            .OrderByDescending(m => m.CreationTime)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(outboxMessage);
    }
}
