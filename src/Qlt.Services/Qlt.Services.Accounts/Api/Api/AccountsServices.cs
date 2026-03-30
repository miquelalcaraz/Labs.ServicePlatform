using MediatR;

using Qlt.Domain.Services;
using Qlt.Platform.Apis;
using Qlt.Services.Accounts.Application.Queries;


namespace Qlt.Services.Accounts.Api
{
    public class AccountsServices(
                    IMediator mediator,
                    IUserHttpContext identityService,
                    ILogger<ApiServices> logger,
                    IOrganizationQueries queries
        ) : ApiServices(mediator, identityService, logger)
    {
        //public IMediator Mediator { get; set; } = mediator;
        //public ILogger<AccountsServices> Logger { get; } = logger;
        //public IUserHttpContext IdentityService { get; } = identityService;
        public IOrganizationQueries Queries { get; } = queries;

    }


}
