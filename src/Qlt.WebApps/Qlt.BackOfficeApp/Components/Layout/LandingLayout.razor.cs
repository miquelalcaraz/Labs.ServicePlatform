using Microsoft.AspNetCore.Components;

namespace Qlt.BackOfficeApp.Components.Layout
{
    public partial class LandingLayout
    {
        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        private void OnSignInClick()
        {
            NavigationManager.NavigateTo("dashboard", true);
        }
    }
}
