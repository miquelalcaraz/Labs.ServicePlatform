using System.Reflection;

namespace Miqa.Contracts.Notifications;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
