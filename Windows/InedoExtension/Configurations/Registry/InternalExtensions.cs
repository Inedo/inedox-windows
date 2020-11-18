using Microsoft.Win32;

namespace Inedo.Extensions.Windows.Configurations.Registry
{
    internal static class InternalExtensions
    {
        public static string GetAbbreviation(this RegistryHive hive)
        {
            switch (hive)
            {
                case RegistryHive.ClassesRoot:
                    return "HKCR";
                case RegistryHive.CurrentUser:
                    return "HKCU";
                case RegistryHive.LocalMachine:
                    return "HKLM";
                case RegistryHive.Users:
                    return "HKU";
                case RegistryHive.CurrentConfig:
                    return "HKCC";
#if NET452
                case RegistryHive.DynData:
                    return "HKDD";
#endif
                case RegistryHive.PerformanceData:
                    return "HKPD";
                default:
                    return "(unknown)";
            }
        }
    }
}
