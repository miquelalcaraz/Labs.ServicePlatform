using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace QltS.Core.Persistence.EF.Extensions
{
    public class QltDbContextConfiguration
    {
        internal List<Assembly> AssembliesToRegister = [];
        public bool UseAuditLogs { get; private set; } = true;
        public bool UseClientRequestIdempotency { get; private set; } = false;
        public string? DefaultSchema { get; private set; } = null;



        public QltDbContextConfiguration RegisterModelsFromAssembly(params Assembly[] assemblies)
        {
            if (assemblies is not null)
            {
                foreach (Assembly assembly in assemblies)
                {
                    AssembliesToRegister.Add(assembly);
                }
            }
            return this;
        }

        /// <summary>
        /// Checks if an assembly is already registered.
        /// </summary>
        /// <param name="assembly">The assembly to check</param>
        /// <returns>True if the assembly is registered, false otherwise</returns>
        public bool IsAssemblyRegistered(Assembly assembly)
        {
            return AssembliesToRegister.Contains(assembly);
        }

        public QltDbContextConfiguration UseAudit()
        {
            UseAuditLogs = true;
            return this;
        }
        public QltDbContextConfiguration UseIdempotency()
        {
            UseClientRequestIdempotency = true;
            return this;
        }

        public QltDbContextConfiguration SetDefaultSchema(string schema)
        {
            DefaultSchema = schema;
            return this;
        }
    }

    /// <summary>
    /// Extension methods for QltDbContextConfiguration.
    /// </summary>
    public static class QltDbContextConfigurationExtensions
    {
        /// <summary>
        /// Gets or creates QltDbContextConfiguration from the service collection.
        /// If it doesn't exist, creates a new instance and registers it as singleton.
        /// If it exists, returns the existing configuration to be modified.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>The QltDbContextConfiguration instance</returns>
        public static QltDbContextConfiguration GetOrCreateQltDbContextConfiguration(this IServiceCollection services)
        {
            // Try to find existing registration
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(QltDbContextConfiguration));

            if (descriptor != null)
            {
                // If it exists as a singleton instance, return it
                if (descriptor.ImplementationInstance is QltDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                // If it exists but not as instance (factory or type), remove it
                services.Remove(descriptor);
            }

            // Create new configuration and register as singleton
            var config = new QltDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }
    }
}
