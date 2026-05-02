using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Qlt.BackOfficeApp.Models;
using Qlt.BackOfficeApp.Services;

using Radzen;

namespace Qlt.BackOfficeApp.Components.Pages.Activation
{
    public partial class List
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected DialogService DialogService { get; set; }

        [Inject]
        protected TooltipService TooltipService { get; set; }

        [Inject]
        protected ContextMenuService ContextMenuService { get; set; }

        [Inject]
        protected NotificationService NotificationService { get; set; }

        [Inject]
        protected SecurityService Security { get; set; }

        private IReadOnlyList<ActivationProcessListItemResponse> items = [];
        private int totalCount;
        private int currentPage = 1;
        private int pageSize = 20;
        private bool isLoading = true;
        private string? errorMessage;

        private string? statusFilter;
        private string? companyFilter;
        private string? emailFilter;

        private readonly List<string> statusOptions =
        [
            "InProgress",
            "WaitingForNotificationDelivery",
            "WaitingForUserActivation",
            "Completed",
            "Failed"
        ];

        protected override async Task OnInitializedAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            errorMessage = null;

            var result = await Api.GetActivationListAsync(
                status: statusFilter,
                companyLegalName: companyFilter,
                adminEmail: emailFilter,
                page: currentPage,
                pageSize: pageSize);

            if (result.IsSuccess)
            {
                items = result.Value!.Items;
                totalCount = result.Value.TotalCount;
            }
            else
            {
                errorMessage = "Failed to load activation processes. Please try again.";
                items = [];
                totalCount = 0;
            }

            isLoading = false;
        }

        private async Task OnPageChanged(PagerEventArgs args)
        {
            currentPage = (args.Skip / args.Top) + 1;
            pageSize = args.Top;
            await LoadDataAsync();
        }

        private async Task RetryAsync(Guid activationProcessId)
        {
            var result = await Api.RetryActivationAsync(activationProcessId);
            if (result.IsSuccess)
            {
                Nav.NavigateTo($"/activation/{activationProcessId}");
            }
            else
            {
                errorMessage = $"Retry failed: {result.ErrorMessage}";
            }
        }

        private async Task ResendAsync(Guid activationProcessId)
        {
            var result = await Api.ResendActivationAsync(activationProcessId);
            if (result.IsSuccess)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Activation Resent",
                    "Activation email has been queued for re-delivery.");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Resend Failed",
                    result.ErrorMessage ?? "Unable to resend activation email.");
            }
        }

        private static BadgeStyle GetStatusBadgeStyle(string status) => status switch
        {
            "Completed" => BadgeStyle.Success,
            "Failed" => BadgeStyle.Danger,
            "InProgress" => BadgeStyle.Info,
            "WaitingForNotificationDelivery" => BadgeStyle.Warning,
            "WaitingForUserActivation" => BadgeStyle.Warning,
            _ => BadgeStyle.Secondary
        };
    }
}
