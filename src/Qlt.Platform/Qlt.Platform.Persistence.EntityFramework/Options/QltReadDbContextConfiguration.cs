using System.Reflection;

namespace Qlt.Platform.Persistence.Options;

/// <summary>
/// Configuration for read-only query contexts. Supports only domain entity mappings and schema.
/// Infrastructure features (idempotency, outbox, audit) are not applicable to read contexts.
/// </summary>
public sealed class QltReadDbContextConfiguration
{
    public List<Assembly> AssembliesToRegister { get; } = [];
    public string? DefaultSchema { get; private set; }

    public QltReadDbContextConfiguration RegisterModelsFromAssembly(params Assembly[] assemblies)
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

    public QltReadDbContextConfiguration SetDefaultSchema(string schema)
    {
        DefaultSchema = schema;
        return this;
    }
}
