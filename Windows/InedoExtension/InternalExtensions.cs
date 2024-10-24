namespace Inedo.Extensions.Windows;

internal static class InternalExtensions
{
    public static string GetAbbreviation(this InedoRegistryHive hive)
    {
        return hive switch
        {
            InedoRegistryHive.ClassesRoot => "HKCR",
            InedoRegistryHive.CurrentUser => "HKCU",
            InedoRegistryHive.LocalMachine => "HKLM",
            InedoRegistryHive.Users => "HKU",
            InedoRegistryHive.CurrentConfig => "HKCC",
            InedoRegistryHive.PerformanceData => "HKPD",
            _ => "(unknown)"
        };
    }
    public static InedoRegistryHive GetInedoHiveRegistry(this string hive)
    {
        return hive switch
        {
            "HKCR" => InedoRegistryHive.ClassesRoot,
            "HKCU" => InedoRegistryHive.CurrentUser,
            "HKLM" => InedoRegistryHive.LocalMachine,
            "HKU" => InedoRegistryHive.Users,
            "HKCC" => InedoRegistryHive.CurrentConfig,
            "HKPD" => InedoRegistryHive.PerformanceData,
            _ => throw new InvalidOperationException($"Cannot identify hive {hive}")
        };
    }
}
