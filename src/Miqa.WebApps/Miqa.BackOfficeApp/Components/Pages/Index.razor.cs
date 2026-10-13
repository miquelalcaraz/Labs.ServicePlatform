using Microsoft.AspNetCore.Components;

namespace Miqa.BackOfficeApp.Components.Pages
{
    public partial class Index
    {
        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        private void OnGetStartedClick()
        {
            NavigationManager.NavigateTo("dashboard", true);
        }
    }
}
