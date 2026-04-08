using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Platform.TestWebApi.Controllers;
using Platform.TestWebApi.Infrastructure;

using System.Net;
using System.Text;
using System.Text.Json;

namespace Platform.FunctionalTests.Tests;

[Collection("BuildingBlocks")]
public sealed class IdempotencyTests
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public IdempotencyTests(BuildingBlocksFixture fixture)
    {
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient();
    }

    [Fact]
    public async Task DuplicateRequest_ShouldReturnSuccess_WithoutCreatingDuplicateEntity()
    {
        var requestId = Guid.NewGuid();
        var entityName = $"IdempotencyTest_{requestId:N}";
        var request = new CreateTestEntityRequest(entityName, 42);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response1 = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var result1Json = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc1 = JsonDocument.Parse(result1Json);
        Assert.True(doc1.RootElement.GetProperty("isSuccess").GetBoolean());

        content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response2 = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var result2Json = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc2 = JsonDocument.Parse(result2Json);
        Assert.True(doc2.RootElement.GetProperty("isSuccess").GetBoolean());

        using var scope = _webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var entitiesCount = await dbContext.TestEntities
            .CountAsync(e => e.Name == entityName, TestContext.Current.CancellationToken);

        Assert.Equal(1, entitiesCount);
    }

    [Fact]
    public async Task DifferentRequests_ShouldCreateDifferentEntities()
    {
        var requestId1 = Guid.NewGuid();
        var request1 = new CreateTestEntityRequest($"Entity1_{requestId1:N}", 10);

        var content1 = new StringContent(
            JsonSerializer.Serialize(request1),
            Encoding.UTF8,
            "application/json");
        content1.Headers.Add("x-requestid", requestId1.ToString());

        var response1 = await _httpClient.PostAsync("api/test", content1, TestContext.Current.CancellationToken);
        Assert.True(response1.IsSuccessStatusCode);

        var result1Json = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc1 = JsonDocument.Parse(result1Json);
        var value1 = doc1.RootElement.GetProperty("value").GetInt32();

        var requestId2 = Guid.NewGuid();
        var request2 = new CreateTestEntityRequest($"Entity2_{requestId2:N}", 20);

        var content2 = new StringContent(
            JsonSerializer.Serialize(request2),
            Encoding.UTF8,
            "application/json");
        content2.Headers.Add("x-requestid", requestId2.ToString());

        var response2 = await _httpClient.PostAsync("api/test", content2, TestContext.Current.CancellationToken);
        Assert.True(response2.IsSuccessStatusCode);

        var result2Json = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc2 = JsonDocument.Parse(result2Json);
        var value2 = doc2.RootElement.GetProperty("value").GetInt32();

        Assert.NotEqual(value1, value2);
    }
}
