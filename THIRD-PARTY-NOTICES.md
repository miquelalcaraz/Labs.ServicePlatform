# Third-Party Notices

This repository is licensed under the [MIT License](LICENSE). It builds on the work of others, listed below.

> [!IMPORTANT]
> Some dependencies are **not free for every use**. This repository is an educational reference; if you reuse it, check the license terms of each dependency against your own context (especially commercial or production use).

## Commercial or dual-licensed dependencies

| Component | Used by | License model | What it means for you |
|---|---|---|---|
| [Duende IdentityServer](https://duendesoftware.com/products/identityserver) 8.x | `Sirius.Infrastructure.Identity` | Commercial license; free for development and testing; a free Community Edition exists for qualifying organizations | Running it in production requires a license that fits your organization. See [Duende licensing](https://duendesoftware.com/products/identityserver#pricing). |
| [MediatR](https://github.com/LuckyPennySoftware/MediatR) 13+ | Platform messaging pipeline | Dual license (Reciprocal Public License 1.5 or commercial), license key required for commercial use; free community edition for qualifying organizations | Configure your own license key if your use requires one. See [Lucky Penny Software](https://luckypennysoftware.com). |
| [FluentAssertions](https://xceed.com/fluent-assertions/) 8.x | Test projects only | Xceed Community License: free for non-commercial use; commercial use requires a paid license | Not shipped with the application. Commercial users should license it or pin to 7.x (Apache-2.0). |

## Source code derived from other projects

| Project | License | Notes |
|---|---|---|
| [dotnet/eShop](https://github.com/dotnet/eShop) | MIT | Architecture and several building blocks (event bus, integration event log, `IdentifiedCommand`, transaction behavior, service defaults) follow eShop's patterns. |
| [Labs.Aspire.uSLearn](https://github.com/miquelalcaraz/Labs.Aspire.uSLearn) | MIT | Predecessor of this repository; the building blocks were first developed there, stage by stage. |
| Duende IdentityServer UI templates | See [Duende templates](https://github.com/DuendeSoftware/products/tree/main/templates) | The Razor pages under `src/Sirius.Infrastructure/Sirius.Infrastructure.Identity/Pages` started from Duende's UI templates. |

## Bundled static assets

| Asset | Location | License |
|---|---|---|
| Bootstrap | `src/Sirius.Infrastructure/Sirius.Infrastructure.Identity/wwwroot/lib/bootstrap` | MIT — see the `LICENSE` file in that folder |
| Bootstrap 4 Glyphicons | `.../wwwroot/lib/bootstrap4-glyphicons` | Apache-2.0 — see the `LICENSE` file in that folder |
| jQuery | `.../wwwroot/lib/jquery` | MIT — see `LICENSE.txt` in that folder |
| Duende logo | `.../wwwroot/duende-logo.svg` | Trademark of Duende Software, used to identify the product |

## Main open-source dependencies (NuGet)

Restored from NuGet at build time; not redistributed in this repository.

| Component | License |
|---|---|
| .NET, ASP.NET Core, Entity Framework Core, .NET Aspire, YARP, Microsoft.Extensions.* | MIT |
| [Radzen.Blazor](https://github.com/radzenhq/radzen-blazor) | MIT |
| [FluentValidation](https://github.com/FluentValidation/FluentValidation) | Apache-2.0 |
| [Scrutor](https://github.com/khellang/Scrutor) | MIT |
| [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet) | Apache-2.0 |
| [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) | MIT |
| [Azure SDK for .NET](https://github.com/Azure/azure-sdk-for-net) (Service Bus, Storage) | MIT |
| [Asp.Versioning](https://github.com/dotnet/aspnet-api-versioning) | MIT |
| [Scalar](https://github.com/scalar/scalar) | MIT |
| [AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks) | Apache-2.0 |
| [Npgsql](https://github.com/npgsql/npgsql) | PostgreSQL License |
| [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) | MIT |
| Test tooling: [xUnit](https://github.com/xunit/xunit) (Apache-2.0), [Moq](https://github.com/devlooped/moq) (BSD-3-Clause), [Testcontainers](https://github.com/testcontainers/testcontainers-dotnet) (MIT), [coverlet](https://github.com/coverlet-coverage/coverlet) (MIT) | — |

For the full list, including transitive dependencies, run:

```bash
dotnet list SiriusSystem.slnx package --include-transitive
```
