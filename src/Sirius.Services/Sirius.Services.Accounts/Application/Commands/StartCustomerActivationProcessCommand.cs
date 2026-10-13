using System.Runtime.Serialization;

using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Accounts.Application.Commands;

[DataContract]
public record StartCustomerActivationProcessCommand : IdempotentCommand<bool>
{
    [DataMember] public Guid SubscriptionId { get; init; }
    [DataMember] public Guid TenantId { get; init; }
    [DataMember] public Guid OwnerCompanyId { get; init; }
    [DataMember] public Guid AdminUserId { get; init; }
    [DataMember] public Guid AdminMembershipId { get; init; }
    [DataMember] public string? CompanyLegalName { get; init; }
    [DataMember] public string? SubscriptionPlanCode { get; init; }
    [DataMember] public string? AdminEmail { get; init; }

    public StartCustomerActivationProcessCommand() { }

    public StartCustomerActivationProcessCommand(
        Guid subscriptionId,
        Guid tenantId,
        Guid ownerCompanyId,
        Guid adminUserId,
        Guid adminMembershipId,
        string? companyLegalName = null,
        string? subscriptionPlanCode = null,
        string? adminEmail = null,
        Guid? requestId = null) : base(requestId)
    {
        SubscriptionId = subscriptionId;
        TenantId = tenantId;
        OwnerCompanyId = ownerCompanyId;
        AdminUserId = adminUserId;
        AdminMembershipId = adminMembershipId;
        CompanyLegalName = companyLegalName;
        SubscriptionPlanCode = subscriptionPlanCode;
        AdminEmail = adminEmail;
    }

    public override Result<bool> CreateResultForDuplicateRequest() => Result.Success(true);
}
