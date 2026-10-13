using System.Reflection;

namespace Miqa.Platform.Persistence.Options
{
    public class MiqaDbContextConfiguration
    {
        public List<Assembly> AssembliesToRegister { get; } = [];
        public bool UseIdempotency { get; private set; } = false;
        public bool UseTransactionalOutbox { get; private set; } = false;
        public string? DefaultSchema { get; private set; } = null;


        public MiqaDbContextConfiguration RegisterModelsFromAssembly(params Assembly[] assemblies)
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

        public MiqaDbContextConfiguration WithIdempotency()
        {
            UseIdempotency = true;
            return this;
        }

        public MiqaDbContextConfiguration WithTransactionalOutbox()
        {
            UseTransactionalOutbox = true;
            return this;
        }
        public MiqaDbContextConfiguration SetDefaultSchema(string schema)
        {
            DefaultSchema = schema;
            return this;
        }
    }
}
