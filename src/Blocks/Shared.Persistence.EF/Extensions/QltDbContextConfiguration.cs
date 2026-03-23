using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace QltS.Shared.Persistence.EF.Extensions
{
    public class QltDbContextConfiguration
    {
        internal List<Assembly> AssembliesToRegister = [];
        internal List<Assembly> EventTypeAssembliesToRegister = [];
        public bool UseAuditLogs { get; private set; } = true;
        public bool UseRequestIdempotency { get; private set; } = false;
        public bool UseIntegrationEventInbox { get; private set; } = false;
        public bool UseTransactionalOutbox { get; private set; } = false;
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

        public QltDbContextConfiguration RegisterIntegrationEventsFromAssembly(params Assembly[] assemblies)
        {
            if (assemblies is not null)
            {
                foreach (Assembly assembly in assemblies)
                {
                    EventTypeAssembliesToRegister.Add(assembly);
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

        public QltDbContextConfiguration WithAuditLogs()
        {
            UseAuditLogs = true;
            return this;
        }
        public QltDbContextConfiguration WithRequestIdempotency()
        {
            UseRequestIdempotency = true;
            return this;
        }
        public QltDbContextConfiguration WithIntegrationEventInbox()
        {
            UseIntegrationEventInbox = true;
            return this;
        }

        public QltDbContextConfiguration WithTransactionalOutbox()
        {
            UseTransactionalOutbox = true;
            return this;
        }
        public QltDbContextConfiguration SetDefaultSchema(string schema)
        {
            DefaultSchema = schema;
            return this;
        }
    }


}
