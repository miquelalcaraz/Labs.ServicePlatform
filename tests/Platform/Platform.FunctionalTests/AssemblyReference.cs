using System.Reflection;

namespace Platform.FunctionalTests
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
