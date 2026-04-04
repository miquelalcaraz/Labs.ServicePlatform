using System.Reflection;

namespace Qlt.Infrastructure.Identity.Contracts;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
