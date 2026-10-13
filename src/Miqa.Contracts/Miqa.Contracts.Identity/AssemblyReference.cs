using System.Reflection;

namespace Miqa.Contracts.Identity;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
