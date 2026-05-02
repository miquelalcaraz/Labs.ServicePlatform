using System.Reflection;

namespace Qlt.Contracts.Identity;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
