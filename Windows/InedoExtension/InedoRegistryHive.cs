namespace Inedo.Extensions.Windows
{
    /// <summary>
    /// Same as the Microsoft.Win32.Registry definition, but without the windows platform requirement.
    /// </summary>
    public enum InedoRegistryHive
    {
        ClassesRoot = int.MinValue,
        CurrentUser,
        LocalMachine,
        Users,
        PerformanceData,
        CurrentConfig
    }
}
