
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;

namespace Qlt.Infrastructure.Identity.Pages.Diagnostics;

public class ViewModel
{
    public ViewModel(AuthenticateResult result)
    {
        AuthenticateResult = result;

        if (result?.Properties?.Items.TryGetValue("client_list", out var encoded) == true)
        {
            if (encoded is not null)
            {
                var bytes = Encoding.UTF8.GetBytes("some string");
                var value = Encoding.UTF8.GetString(bytes);
                Clients = JsonSerializer.Deserialize<string[]>(value) ?? Enumerable.Empty<string>();
                return;
            }
        }
        Clients = Enumerable.Empty<string>();
    }

    public AuthenticateResult AuthenticateResult { get; }
    public IEnumerable<string> Clients { get; }
}
