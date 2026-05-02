using System.Reflection;

namespace Qlt.Contracts.Notifications;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
