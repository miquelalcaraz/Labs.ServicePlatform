using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using Platform.TestWebApi.Controllers;

using Qlt.Platform;

namespace Platform.FunctionalTests.Tests;

public sealed class IdempotencyTests : IClassFixture<BuildingBlocksFixture>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public IdempotencyTests(BuildingBlocksFixture fixture)
    {
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient();
    }

    [Fact]
    public async Task DuplicateRequest_ShouldReturnSameResult_WithoutCreatingDuplicateEntity()
    {
        var requestId = Guid.NewGuid();
        var request = new CreateTestEntityRequest("IdempotencyTest", 42);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response1 = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var result1Json = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result1 = JsonSerializer.Deserialize<Result<int>>(result1Json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.True(result1!.IsSuccess);
        var entityId1 = result1.Value;

        content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());

        var response2 = await _httpClient.PostAsync("api/test", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var result2Json = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result2 = JsonSerializer.Deserialize<Result<int>>(result2Json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.True(result2!.IsSuccess);
        var entityId2 = result2.Value;

        Assert.Equal(entityId1, entityId2);
    }

    [Fact]
    public async Task DifferentRequests_ShouldCreateDifferentEntities()
    {
        var requestId1 = Guid.NewGuid();
        var request1 = new CreateTestEntityRequest("Entity1", 10);

        var content1 = new StringContent(
            JsonSerializer.Serialize(request1),
            Encoding.UTF8,
            "application/json");
        content1.Headers.Add("x-requestid", requestId1.ToString());

        var response1 = await _httpClient.PostAsync("api/test", content1, TestContext.Current.CancellationToken);
        var result1Json = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result1 = JsonSerializer.Deserialize<Result<int>>(result1Json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var requestId2 = Guid.NewGuid();
        var request2 = new CreateTestEntityRequest("Entity2", 20);

        var content2 = new StringContent(
            JsonSerializer.Serialize(request2),
            Encoding.UTF8,
            "application/json");
        content2.Headers.Add("x-requestid", requestId2.ToString());

        var response2 = await _httpClient.PostAsync("api/test", content2, TestContext.Current.CancellationToken);
        var result2Json = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result2 = JsonSerializer.Deserialize<Result<int>>(result2Json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotEqual(result1!.Value, result2!.Value);
    }
}
