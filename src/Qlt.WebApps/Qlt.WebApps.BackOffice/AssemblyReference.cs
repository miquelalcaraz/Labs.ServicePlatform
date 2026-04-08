using System.Reflection;



public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    public static readonly string Version = Assembly.GetName().Version.ToString();

    public static string? GetBuildDate() =>
        typeof(AssemblyReference).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")
            ?.Value;

}

