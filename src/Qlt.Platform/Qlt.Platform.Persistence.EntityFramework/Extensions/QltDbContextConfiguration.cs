using System.Reflection;

namespace Qlt.Platform.Persistence.Extensions
{
    public class QltDbContextConfiguration
    {
        public List<Assembly> AssembliesToRegister = [];
        public bool UseAuditLogs { get; private set; } = true;
        public bool UseIdempotency { get; private set; } = false;
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

        /// <summary>
        /// Checks if an assembly is already registered.
        /// </summary>
        /// <param name="assembly">The assembly to check</param>
        /// <returns>True if the assembly is registered, false otherwise</returns>
        internal bool IsAssemblyRegistered(Assembly assembly)
        {
            return AssembliesToRegister.Contains(assembly);
        }

        public QltDbContextConfiguration WithAuditLogs()
        {
            UseAuditLogs = true;
            return this;
        }
        public QltDbContextConfiguration WithIdempotency()
        {
            UseIdempotency = true;
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
