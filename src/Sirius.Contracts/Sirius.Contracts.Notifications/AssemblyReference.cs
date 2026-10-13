using System.Reflection;

namespace Sirius.Contracts.Notifications;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
