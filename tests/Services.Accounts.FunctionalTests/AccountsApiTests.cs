using Asp.Versioning;
using Asp.Versioning.Http;

using Microsoft.AspNetCore.Mvc.Testing;

using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;

using System.Net;
using System.Text;
using System.Text.Json;

namespace Services.Accounts.FunctionalTests;

public sealed class AccountsApiTests : IClassFixture<AccountsApiFixture>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public AccountsApiTests(AccountsApiFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));

        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);


    }


    [Fact]
    public async Task CreateOrganizationSucceeds()
    {
        var payload = FakeCreateOrganization();
        var content = new StringContent(JsonSerializer.Serialize(FakeCreateOrganization()), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PutAsync("api/accounts", content, TestContext.Current.CancellationToken);

        //var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        //var responseData = JsonSerializer.Deserialize<Result<bool>>(s, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrganizationWithSameRequestIdReturnsTrueAndOk()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var payload = FakeCreateOrganization();
        var json = JsonSerializer.Serialize(payload);

        // Act - First request (creates organization)
        var content1 = new StringContent(json, UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", requestId.ToString() } }
        };
        var response1 = await _httpClient.PutAsync("api/accounts", content1, TestContext.Current.CancellationToken);

        // Act - Second request (duplicate, should be idempotent)
        var content2 = new StringContent(json, UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", requestId.ToString() } }
        };
        var response2 = await _httpClient.PutAsync("api/accounts", content2, TestContext.Current.CancellationToken);

        // Assert - Both requests return 200 OK
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Assert - Both responses have body with 'true'
        var body1 = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body2 = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("true", body1);
        Assert.Equal("true", body2);
    }

    private CreateOrganizationCommand FakeCreateOrganization()
    {
        return new CreateOrganizationCommand(
                taxIdNumber: "B12345678",
                taxNumberType: TaxNumberType.TaxId,
                name: "Test Company",
                legalName: "Test Company S.L.",
                street: "Calle de Prueba 123",
                city: "Barcelona",
                state: "Barcelona",
                country: "ES",
                zipCode: "08001",
                organizationType: OrganizationType.Company
            );

    }
}
