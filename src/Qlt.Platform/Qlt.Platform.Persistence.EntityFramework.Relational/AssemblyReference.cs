using System.Reflection;

namespace Qlt.Platform.Persistence;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    public static readonly string AssemblyName = typeof(AssemblyReference).GetTypeInfo().Assembly.GetName().Name!;
}

