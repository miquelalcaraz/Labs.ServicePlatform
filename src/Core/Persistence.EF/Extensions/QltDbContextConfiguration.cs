using System.Reflection;

namespace QltSystem.Core.Persistence.EF.Extensions
{
    public class QltDbContextConfiguration
    {
        internal List<Assembly> AssembliesToRegister = [];
        public bool UseAuditLogs { get; private set; } = true;
        public bool UseClientRequestIdempotency { get; private set; } = false;
        public bool UseIntegrationEventLogs { get; private set; } = false;
        public bool UseEventIdempotency { get; private set; } = false;
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
        public QltDbContextConfiguration UseAudit()
        {
            UseAuditLogs = true;
            return this;
        }
        public QltDbContextConfiguration UseIdempotency()
        {
            UseClientRequestIdempotency = true;
            UseIntegrationEventLogs = true;
            UseEventIdempotency = true;
            return this;
        }

        public QltDbContextConfiguration SetDefaultSchema(string schema)
        {
            DefaultSchema = schema;
            return this;
        }
    }
}
