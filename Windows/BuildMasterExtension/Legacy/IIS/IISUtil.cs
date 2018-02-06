using System;
using System.Collections.Generic;
using System.DirectoryServices;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    /// <summary>
    /// Provides methods for interfacing with IIS.
    /// </summary>
    internal abstract partial class IISUtil
    {
        private static readonly object instanceLock = new object();
        private static WeakReference instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="IISUtil"/> class.
        /// </summary>
        protected IISUtil()
        {
        }

        /// <summary>
        /// Gets the IIS management instance.
        /// </summary>
        public static IISUtil Instance
        {
            get
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        var inst = CreateInstance();
                        instance = new WeakReference(inst);
                        return inst;
                    }

                    var current = instance.Target as IISUtil;
                    if (current == null)
                    {
                        current = CreateInstance();
                        instance = new WeakReference(current);
                    }

                    return current;
                }
            }
        }

        /// <summary>
        /// Returns a collection of the names of all AppPools on the local system.
        /// </summary>
        /// <returns>Names of all of the AppPools on the local system.</returns>
        public abstract IEnumerable<string> GetAppPoolNames();
        /// <summary>
        /// Starts an AppPool on the local system.
        /// </summary>
        /// <param name="name">Name of the AppPool to start.</param>
        public abstract void StartAppPool(string name);
        /// <summary>
        /// Stops an AppPool on the local system.
        /// </summary>
        /// <param name="name">Name of the AppPool to stop.</param>
        public abstract void StopAppPool(string name);
        /// <summary>
        /// Creates a new application pool.
        /// </summary>
        /// <param name="name">The name of the application pool.</param>
        /// <param name="user">The user account of the application pool.</param>
        /// <param name="password">The password for the specified user account.</param>
        /// <param name="integratedMode">If true, sets the app pool mode to integrated.</param>
        /// <param name="managedRuntimeVersion">The version of .NET hosting the app pool.</param>
        public abstract void CreateAppPool(string name, string user, string password, bool integratedMode, string managedRuntimeVersion);

        /// <summary>
        /// Check if an IIS Application Pool of name <paramref name="appPoolName"/> exists.
        /// </summary>
        /// <param name="appPoolName">The name of the IIS Application Pool to look for</param>
        /// <returns>True if the AppPool already exists else false</returns>
        public abstract bool AppPoolExists(string appPoolName);


        /// <summary>
        /// Creates a new website.
        /// </summary>
        /// <param name="name">The name of the website.</param>
        /// <param name="path">The physical path of the website.</param>
        /// <param name="appPool">The name of the application pool.</param>
        /// <param name="https">Binds to HTTPS instead of HTTP</param>
        /// <param name="binding">The port, hostname, and IP of the website.</param>
        public abstract void CreateWebSite(string name, string path, string appPool, bool https, BindingInfo binding);

        /// <summary>
        /// Check if an IIS hosted website of name <paramref name="name"/> exists.
        /// </summary>
        /// <param name="name">The name of the website to look for in the IIS</param>
        /// <returns>True if website already exists else false</returns>
        public abstract bool WebSiteExists(string name);



        /// <summary>
        /// Returns a new instances of the newest supported IIS management interface.
        /// </summary>
        /// <returns>Instance of the newest supported IIS management interface.</returns>
        private static IISUtil CreateInstance()
        {
            try
            {
                return new IIS7Util();
            }
            catch
            {
            }

            try
            {
                return new IIS6Util();
            }
            catch
            {
            }

            throw new InvalidOperationException("IIS 6 or newer management interfaces are not present on the system.");
        }

        protected void AddUserToGroup(string userName)
        {
            if (!userName.Contains("\\"))
                userName = Environment.MachineName + "\\" + userName;

            var AD = new DirectoryEntry("WinNT://" + Environment.MachineName + ",computer");
            var userPath = "WinNT://" + userName.Replace('\\', '/') + ",user";

            DirectoryEntry group1 = null;
            try
            {
                group1 = AD.Children.Find("IIS_WPG", "group");
                if (group1 != null)
                    group1.Invoke("Add", new object[] { userPath });
            }
            catch
            {
            }

            DirectoryEntry group2 = null;
            try
            {
                group2 = AD.Children.Find("IIS_IUSRS", "group");
                if (group2 != null)
                    group2.Invoke("Add", new object[] { userPath });
            }
            catch
            {
            }
        }

        internal class BindingInfo
        {
            public int Port { get; set; }
            public string HostName { get; set; }
            public string IPAddress { get; set; }

            public BindingInfo(string hostName, int port, string ipAddress)
            {
                this.HostName = hostName;
                this.IPAddress = string.IsNullOrEmpty(ipAddress) ? "*" : ipAddress;
                this.Port = port;
            }

            public override string ToString()
            {
                return string.Format("{0}:{1}:{2}", this.IPAddress, this.Port, this.HostName);
            }
        }
    }
}
