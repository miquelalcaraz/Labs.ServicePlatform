using System.Reflection;

namespace QltS.Core.Persistence.EF.Relational;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    public static readonly string AssemblyName = typeof(AssemblyReference).GetTypeInfo().Assembly.GetName().Name!;
}

