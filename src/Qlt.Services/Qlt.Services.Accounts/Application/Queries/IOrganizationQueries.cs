namespace Qlt.Services.Accounts.Application.Queries;

public interface IOrganizationQueries
{
    Task<Organization?> GetOrganizationAsync(Guid id);
    Task<IEnumerable<OrganizationSummary>> GetAllOrganizationsAsync();

}
