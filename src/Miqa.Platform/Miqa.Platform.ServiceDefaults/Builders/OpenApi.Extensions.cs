using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Miqa.Platform.Extensions;

using Scalar.AspNetCore;

namespace Miqa.Platform.ServiceDefaults
{

    public static partial class Extensions
    {
        public static IApplicationBuilder UseDefaultOpenApi(this WebApplication app)
        {
            var configuration = app.Configuration;
            var openApiSection = configuration.GetSection("OpenApi");

            if (!openApiSection.Exists())
            {
                return app;
            }

            app.MapOpenApi();

            if (app.Environment.IsDevelopment())
            {
                app.MapScalarApiReference(options =>
                {
                    options.DefaultFonts = false;
                });
                app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
            }

            return app;
        }

        public static IHostApplicationBuilder AddDefaultOpenApi(
            this IHostApplicationBuilder builder,
            IApiVersioningBuilder? apiVersioning = default)
        {
            var openApi = builder.Configuration.GetSection("OpenApi");
            var identitySection = builder.Configuration.GetSection("Identity");

            var scopes = identitySection.Exists()
                ? identitySection.GetRequiredSection("Scopes").GetChildren().ToDictionary(p => p.Key, p => p.Value)
                : new Dictionary<string, string?>();


            if (!openApi.Exists())
            {
                return builder;
            }

            if (apiVersioning is not null)
            {
                var versioned = apiVersioning.AddApiExplorer(options => options.GroupNameFormat = "'v'VVV");
                string[] versions = ["v1", "v2"];
                foreach (var description in versions)
                {
                    builder.Services.AddOpenApi(description, options =>
                    {
                        options.ApplyApiVersionInfo(openApi.GetRequiredValue("Document:Title"), openApi.GetRequiredValue("Document:Description"));
                        options.ApplyAuthorizationChecks([.. scopes.Keys]);
                        options.ApplySecuritySchemeDefinitions();
                        options.ApplyOperationDeprecatedStatus();
                        options.ApplyApiVersionDescription();
                    });
                }
            }

            return builder;
        }
    }
}
