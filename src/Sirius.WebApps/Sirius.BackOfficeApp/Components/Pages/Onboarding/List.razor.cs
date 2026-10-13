using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Sirius.BackOfficeApp.Models;
using Sirius.BackOfficeApp.Services;

using Radzen;

namespace Sirius.BackOfficeApp.Components.Pages.Onboarding
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

        private IReadOnlyList<OnboardingProcessListItemResponse> items = [];
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
        "WaitingForExternalProvisioning",
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

            var result = await Api.GetOnboardingListAsync(
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
                errorMessage = "Failed to load onboarding processes. Please try again.";
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

        private async Task RetryAsync(Guid onboardingProcessId)
        {
            var result = await Api.RetryOnboardingAsync(onboardingProcessId);
            if (result.IsSuccess)
            {
                Nav.NavigateTo($"/onboarding/{onboardingProcessId}");
            }
            else
            {
                errorMessage = $"Retry failed: {result.ErrorMessage}";
            }
        }

        private static BadgeStyle GetStatusBadgeStyle(string status) => status switch
        {
            "Completed" => BadgeStyle.Success,
            "Failed" => BadgeStyle.Danger,
            "InProgress" => BadgeStyle.Info,
            "WaitingForExternalProvisioning" or "WaitingForActivationDelivery" => BadgeStyle.Warning,
            "WaitingForUserActivation" => BadgeStyle.Light,
            _ => BadgeStyle.Secondary
        };
    }
}

