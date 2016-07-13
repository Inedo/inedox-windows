using System;
using System.Collections.Generic;
using System.DirectoryServices;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    internal partial class IISUtil
    {
        /// <summary>
        /// Provides methods for interfacing with IIS 6.
        /// </summary>
        private sealed class IIS6Util : IISUtil
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="IIS6Util"/> class.
            /// </summary>
            public IIS6Util()
            {
                ValidateIis6Management();
            }

            /// <summary>
            /// Returns a collection of the names of all AppPools on the local system.
            /// </summary>
            /// <returns>Names of all of the AppPools on the local system.</returns>
            public override IEnumerable<string> GetAppPoolNames()
            {
                var poolNames = new List<string>();
                using (DirectoryEntry W3SVC = new DirectoryEntry("IIS://localhost/w3svc"))
                {
                    foreach (DirectoryEntry Site in W3SVC.Children)
                    {
                        if (Site.SchemaClassName != "IIsApplicationPools")
                            continue;

                        foreach (DirectoryEntry child in Site.Children)
                            poolNames.Add(child.Name);
                    }
                }

                return poolNames;
            }
            /// <summary>
            /// Starts an AppPool on the local system.
            /// </summary>
            /// <param name="name">Name of the AppPool to start.</param>
            public override void StartAppPool(string name)
            {
                DoAppPoolAction(name, AppPoolCommands.StartAppPool);
            }
            /// <summary>
            /// Stops an AppPool on the local system.
            /// </summary>
            /// <param name="name">Name of the AppPool to stop.</param>
            public override void StopAppPool(string name)
            {
                DoAppPoolAction(name, AppPoolCommands.StopAppPool);
            }

            public override void CreateAppPool(string name, string user, string password, bool integratedMode, string managedRuntimeVersion)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Check if an IIS Application Pool of name <paramref name="appPoolName"/> exists.
            /// </summary>
            /// <param name="appPoolName">The name of the IIS Application Pool to look for</param>
            /// <returns>True if the AppPool already exists else false</returns>
            public override bool AppPoolExists(string appPoolName)
            {
                throw new NotSupportedException();
            }

            public override void CreateWebSite(string name, string path, string appPool, bool https, BindingInfo binding)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Check if an IIS hosted website of name <paramref name="name"/> exists.
            /// </summary>
            /// <param name="name">The name of the website to look for in the IIS</param>
            /// <returns>True if website already exists else false</returns>
            public override bool WebSiteExists(string name)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Verifies that the required IIS6 interfaces are present.
            /// </summary>
            private void ValidateIis6Management()
            {
                try
                {
                    using (DirectoryEntry W3SVC = new DirectoryEntry("IIS://localhost/w3svc"))
                    {
                        W3SVC.Children.GetEnumerator();
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("IIS 6 Management Interface is not available.", e);
                }
            }
            /// <summary>
            /// Runs an AppPool command.
            /// </summary>
            /// <param name="appPoolName">Name of the AppPool.</param>
            /// <param name="action">AppPool command to run.</param>
            private void DoAppPoolAction(string appPoolName, AppPoolCommands action)
            {
                ValidateIis6Management();

                bool isFound = false;

                using (DirectoryEntry W3SVC = new DirectoryEntry("IIS://localhost/w3svc"))
                {
                    foreach (DirectoryEntry Site in W3SVC.Children)
                    {
                        if (Site.SchemaClassName != "IIsApplicationPools") continue;
                        foreach (DirectoryEntry child in Site.Children)
                        {
                            if (child.Name != appPoolName) continue;

                            PropertyCollection appPoolProps = child.Properties;
                            appPoolProps["AppPoolCommand"].Value = (int)action;
                            child.CommitChanges();
                            isFound = true;
                        }
                    }
                }

                if (!isFound)
                    throw new InvalidOperationException("App Pool Not Found.");
            }

            /// <summary>
            /// Recognized AppPool commands.
            /// </summary>
            private enum AppPoolCommands
            {
                /// <summary>
                /// Starts the AppPool.
                /// </summary>
                StartAppPool = 1,
                /// <summary>
                /// Stops the AppPool.
                /// </summary>
                StopAppPool = 2
            }
        }
    }
}
