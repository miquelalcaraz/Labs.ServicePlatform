using System.Reflection;

namespace Miqa.Platform.Persistence.Options;

/// <summary>
/// Configuration for read-only query contexts. Supports only domain entity mappings and schema.
/// Infrastructure features (idempotency, outbox, audit) are not applicable to read contexts.
/// </summary>
public sealed class MiqaReadDbContextConfiguration
{
    public List<Assembly> AssembliesToRegister { get; } = [];
    public string? DefaultSchema { get; private set; }

    public MiqaReadDbContextConfiguration RegisterModelsFromAssembly(params Assembly[] assemblies)
    {
        if (assemblies is not null)
        {
            foreach (var assembly in assemblies)
            {
                AssembliesToRegister.Add(assembly);
            }
        }
        return this;
    }

    public MiqaReadDbContextConfiguration SetDefaultSchema(string schema)
    {
        DefaultSchema = schema;
        return this;
    }
}
