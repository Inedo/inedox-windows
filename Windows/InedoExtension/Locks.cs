namespace Inedo.Extensions.Windows
{
    /// <summary>
    /// Process-wide locks for synchronizing access to certain resources.
    /// This helps to reduce the frequency of a certain class of errors.
    /// </summary>
    internal static class Locks
    {
        public static readonly object IIS = new();
    }
}
