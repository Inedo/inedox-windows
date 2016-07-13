namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    /// <summary>
    /// Represents an <see cref="ActionBase"/> that refers to an IIS application pool.
    /// </summary>
    internal interface IIISAppPoolAction
    {
        /// <summary>
        /// Gets or sets the name of the application pool.
        /// </summary>
        string AppPool { get; set; }
    }
}
