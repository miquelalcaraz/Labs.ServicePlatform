using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Miqa.BackOfficeApp.Models;
using Miqa.BackOfficeApp.Services;

using Radzen;

namespace Miqa.BackOfficeApp.Components.Pages.Onboarding
{
    public partial class Result
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

        [Parameter]
        public Guid OnboardingProcessId { get; set; }

        private OnboardingProcessResultResponse? result;
        private bool isLoading = true;
        private bool isNotReady;
        private string? errorMessage;

        protected override async Task OnInitializedAsync()
        {
            var apiResult = await Api.GetOnboardingResultAsync(OnboardingProcessId);

            if (apiResult.IsSuccess)
            {
                result = apiResult.Value;
            }
            else if (apiResult.StatusCode == 400)
            {
                isNotReady = true;
                errorMessage = "The onboarding result is not available yet. The process must complete first.";
            }
            else if (apiResult.StatusCode == 404)
            {
                errorMessage = "Onboarding process not found.";
            }
            else
            {
                errorMessage = "Failed to load onboarding result.";
            }

            isLoading = false;
        }
    }
}