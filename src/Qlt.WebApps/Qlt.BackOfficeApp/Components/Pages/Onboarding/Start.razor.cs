using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Qlt.BackOfficeApp.Models;
using Qlt.BackOfficeApp.Services;

using Radzen;

namespace Qlt.BackOfficeApp.Components.Pages.Onboarding
{
    public partial class Start
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

        private StartOnboardingRequest model = new();
        private bool isSubmitting;
        private string? errorMessage;

        private readonly List<string> languages = ["en", "es", "fr", "de", "pt", "it"];

        private async Task OnSubmitAsync()
        {
            isSubmitting = true;
            errorMessage = null;

            try
            {
                var result = await Api.StartOnboardingAsync(model);

                if (result.IsSuccess)
                {
                    Notification.Notify(NotificationSeverity.Success, "Onboarding Started",
                        $"Process {result.Value!.OnboardingProcessId} has been initiated.");
                    Nav.NavigateTo($"/onboarding/{result.Value.OnboardingProcessId}");
                }
                else
                {
                    errorMessage = result.ErrorMessage ?? "Failed to start onboarding. Please check your input and try again.";
                }
            }
            finally
            {
                isSubmitting = false;
            }
        }
    }
}
