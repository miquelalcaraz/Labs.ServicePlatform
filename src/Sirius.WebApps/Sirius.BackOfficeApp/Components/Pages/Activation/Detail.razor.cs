using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Sirius.BackOfficeApp.Models;
using Sirius.BackOfficeApp.Services;

using Radzen;

namespace Sirius.BackOfficeApp.Components.Pages.Activation
{
    public partial class Detail
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
        public Guid ActivationProcessId { get; set; }

        private ActivationProcessDetailResponse? detail;
        private bool isLoading = true;
        private bool isRetrying;
        private bool isResending;
        private bool isPolling;
        private string? errorMessage;
        private int pollingIntervalSeconds = 5;

        private CancellationTokenSource? pollingCts;

        protected override async Task OnInitializedAsync()
        {
            await LoadDetailAsync();
            StartPollingIfNeeded();
        }

        private async Task LoadDetailAsync()
        {
            var result = await Api.GetActivationDetailAsync(ActivationProcessId);
            if (result.IsSuccess)
            {
                detail = result.Value;
                errorMessage = null;
            }
            else
            {
                errorMessage = result.StatusCode == 404
                    ? "Activation process not found."
                    : "Failed to load activation detail.";
            }

            isLoading = false;
        }

        private async Task RefreshAsync()
        {
            await LoadDetailAsync();
            StartPollingIfNeeded();
            StateHasChanged();
        }

        private async Task RetryAsync()
        {
            isRetrying = true;
            StateHasChanged();

            var result = await Api.RetryActivationAsync(ActivationProcessId);
            if (result.IsSuccess)
            {
                Notification.Notify(NotificationSeverity.Success, "Retry Initiated",
                    $"Retry #{result.Value!.RetryCount} has been started.");
                await LoadDetailAsync();
                StartPollingIfNeeded();
            }
            else
            {
                Notification.Notify(NotificationSeverity.Error, "Retry Failed",
                    result.ErrorMessage ?? "Unable to retry activation.");
            }

            isRetrying = false;
            StateHasChanged();
        }

        private async Task ResendAsync()
        {
            isResending = true;
            StateHasChanged();

            var result = await Api.ResendActivationAsync(ActivationProcessId);
            if (result.IsSuccess)
            {
                Notification.Notify(NotificationSeverity.Success, "Activation Resent",
                    "Activation email has been queued for re-delivery.");
                await LoadDetailAsync();
            }
            else
            {
                Notification.Notify(NotificationSeverity.Error, "Resend Failed",
                    result.ErrorMessage ?? "Unable to resend activation email.");
            }

            isResending = false;
            StateHasChanged();
        }

        private void StartPollingIfNeeded()
        {
            StopPolling();

            if (detail is null) return;
            if (!ShouldPoll(detail.Status)) return;

            pollingIntervalSeconds = GetPollingInterval(detail.Status);
            isPolling = true;
            pollingCts = new CancellationTokenSource();

            _ = PollLoopAsync(pollingCts.Token);
        }

        private async Task PollLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), cancellationToken);

                    if (cancellationToken.IsCancellationRequested) break;

                    var result = await Api.GetActivationDetailAsync(ActivationProcessId, cancellationToken);
                    if (result.IsSuccess && !cancellationToken.IsCancellationRequested)
                    {
                        detail = result.Value;

                        var newInterval = GetPollingInterval(detail!.Status);
                        if (newInterval != pollingIntervalSeconds)
                            pollingIntervalSeconds = newInterval;

                        await InvokeAsync(StateHasChanged);

                        if (!ShouldPoll(detail.Status))
                        {
                            isPolling = false;
                            await InvokeAsync(StateHasChanged);
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on disposal
            }
            catch (Exception)
            {
                isPolling = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private void StopPolling()
        {
            isPolling = false;
            pollingCts?.Cancel();
            pollingCts?.Dispose();
            pollingCts = null;
        }

        private static bool ShouldPoll(string status)
            => status is "InProgress" or "WaitingForNotificationDelivery" or "WaitingForUserActivation";

        private static int GetPollingInterval(string status) => status switch
        {
            "InProgress" => 5,
            "WaitingForNotificationDelivery" => 10,
            "WaitingForUserActivation" => 30,
            _ => 5
        };

        private static BadgeStyle GetStatusBadgeStyle(string status) => status switch
        {
            "Completed" => BadgeStyle.Success,
            "Failed" => BadgeStyle.Danger,
            "InProgress" => BadgeStyle.Info,
            "WaitingForNotificationDelivery" => BadgeStyle.Warning,
            "WaitingForUserActivation" => BadgeStyle.Warning,
            _ => BadgeStyle.Secondary
        };

        private static ProgressBarStyle GetProgressBarStyle(string status) => status switch
        {
            "Completed" => ProgressBarStyle.Success,
            "Failed" => ProgressBarStyle.Danger,
            _ => ProgressBarStyle.Primary
        };

        private static BadgeStyle GetStepBadgeStyle(string stepStatus) => stepStatus switch
        {
            "Completed" => BadgeStyle.Success,
            "Current" => BadgeStyle.Info,
            "Failed" => BadgeStyle.Danger,
            _ => BadgeStyle.Light
        };

        public async ValueTask DisposeAsync()
        {
            StopPolling();
            await ValueTask.CompletedTask;
        }
    }
}
