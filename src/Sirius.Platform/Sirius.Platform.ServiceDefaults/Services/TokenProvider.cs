namespace Sirius.Platform.Services;

/// <summary>
/// Scoped service that holds the access token for the duration of a request or Blazor Server circuit.
/// Populated during SSR by the root component; consumed by <see cref="Sirius.Platform.Builders.HttpClientExtensions"/>.
/// </summary>
public sealed class TokenProvider
{
    public string? AccessToken { get; set; }
}
