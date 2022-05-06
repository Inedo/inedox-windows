namespace Inedo.Extensions.Windows
{
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
    }
}
