using System.Reflection;

namespace Qlt.Services.Accounts.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}

