using System.Net;
using System.Text;
using System.Text.Json;

using Asp.Versioning;
using Asp.Versioning.Http;

using Microsoft.AspNetCore.Mvc.Testing;

using Miqa.Services.Accounts.Application.Commands;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Services.FunctionalTests.Accounts;

public sealed class AccountsApiTests : IClassFixture<AccountsFixture>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public AccountsApiTests(AccountsFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));

        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task CreateCompanySucceeds()
    {
        var payload = FakeCreateCompany();
        var content = new StringContent(JsonSerializer.Serialize(payload), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PutAsync("api/accounts/companies", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateCompanyWithSameRequestIdReturnsTrueAndOk()
    {
        var requestId = Guid.NewGuid();
        var payload = FakeCreateCompany();
        var json = JsonSerializer.Serialize(payload);

        var content1 = new StringContent(json, UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", requestId.ToString() } }
        };
        var response1 = await _httpClient.PutAsync("api/accounts/companies", content1, TestContext.Current.CancellationToken);

        var content2 = new StringContent(json, UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", requestId.ToString() } }
        };
        var response2 = await _httpClient.PutAsync("api/accounts/companies", content2, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var body1 = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body2 = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("true", body1);
        Assert.Equal("true", body2);
    }

    private CreateCompanyCommand FakeCreateCompany()
    {
        return new CreateCompanyCommand(
            tenantId: Guid.NewGuid(),
            legalName: "Test Company S.L.",
            tradeName: "Test Company",
            taxId: "B12345678",
            country: "Spain",
            countryCode: "ES",
            companyType: CompanyType.Owner,
            taxNumberType: TaxNumberType.TaxId,
            state: "Barcelona",
            postalCode: "08001",
            city: "Barcelona",
            street: "Calle de Prueba 123");
    }
}
