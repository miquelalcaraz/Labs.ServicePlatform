using MediatR;

using QltS.Accounts.Application.Queries;
using QltS.Core.API.Apis;
using QltS.Core.Application.Services;


namespace QltS.Accounts.Api
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
