using MediatR;

using QltSystem.Accounts.Application.Queries;
using QltSystem.Core.API.Apis;
using QltSystem.Core.API.Services;


namespace QltSystem.Accounts.Api
{
    public class AccountsServices(
                    IMediator mediator,
                    IIdentityService identityService,
                    ILogger<AccountsServices> logger,
                    IOrganizationQueries queries
        ) //: ApiServices(mediator, identityService, logger)
    {
        public IMediator Mediator { get; set; } = mediator;
        public ILogger<AccountsServices> Logger { get; } = logger;
        public IIdentityService IdentityService { get; } = identityService;
        public IOrganizationQueries Queries { get; } = queries;

    }


}
