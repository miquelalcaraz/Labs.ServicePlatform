using System.Reflection;

namespace Sirius.Services.Notifications.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}

