using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Inedo.Diagnostics;
using Microsoft.Web.Administration;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    internal partial class IISUtil
    {
        /// <summary>
        /// Provides methods for interfacing with IIS 7.
        /// </summary>
        private sealed class IIS7Util : IISUtil
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="IIS7Util"/> class.
            /// </summary>
            public IIS7Util()
            {
                ValidateIis7Management();
            }

            /// <summary>
            /// Returns a collection of the names of all AppPools on the local system.
            /// </summary>
            /// <returns>Names of all of the AppPools on the local system.</returns>
            public override IEnumerable<string> GetAppPoolNames()
            {
                using (var manager = new ServerManager())
                {
                    foreach (var pool in manager.ApplicationPools)
                        yield return pool.Name;
                }
            }

            /// <summary>
            /// Starts an AppPool on the local system.
            /// </summary>
            /// <param name="name">Name of the AppPool to start.</param>
            public override void StartAppPool(string name)
            {
                using (var manager = new ServerManager())
                {
                    var pool = manager.ApplicationPools[name];
                    if (pool == null)
                        throw new IISException("Application pool not found.");

                    try
                    {
                        pool.Start();
                    }
                    catch (Exception ex)
                    {
                        throw new IISException("Could not start application pool: " + ex.Message, ex);
                    }
                }
            }

            /// <summary>
            /// Stops an AppPool on the local system.
            /// </summary>
            /// <param name="name">Name of the AppPool to stop.</param>
            public override void StopAppPool(string name)
            {
                using (var manager = new ServerManager())
                {
                    var pool = manager.ApplicationPools[name];
                    if (pool == null)
                        throw new IISException("Application pool not found.", MessageLevel.Warning);

                    try
                    {
                        pool.Stop();
                    }
                    catch (COMException ex) when ((uint)ex.ErrorCode == 0x80070426)
                    {
                        throw new IISException("Application pool is already stopped.", ex, MessageLevel.Information);
                    }
                    catch (Exception ex)
                    {
                        throw new IISException("Could not stop application pool: " + ex.Message);
                    }
                }
            }

            public override void CreateAppPool(string name, string user, string password, bool integratedMode,
                string managedRuntimeVersion)
            {
                using (var manager = new ServerManager())
                {
                    var appPool = manager.ApplicationPools.Add(name);
                    appPool.ManagedPipelineMode = integratedMode
                        ? ManagedPipelineMode.Integrated
                        : ManagedPipelineMode.Classic;
                    appPool.ManagedRuntimeVersion = managedRuntimeVersion;

                    switch (user)
                    {
                        case "LocalSystem":
                            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.LocalSystem;
                            break;

                        case "LocalService":
                            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.LocalService;
                            break;

                        case "NetworkService":
                            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                            break;

                        case "ApplicationPoolIdentity":
                            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
                            break;

                        default:
                            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
                            appPool.ProcessModel.UserName = user;
                            appPool.ProcessModel.Password = password;
                            AddUserToGroup(user);
                            break;
                    }

                    manager.CommitChanges();
                }
            }

            /// <summary>
            /// Check if an IIS Application Pool of name <paramref name="appPoolName"/> exists.
            /// </summary>
            /// <param name="appPoolName">The name of the IIS Application Pool to look for</param>
            /// <returns>True if the AppPool already exists else false</returns>
            public override bool AppPoolExists(string appPoolName)
            {
                return this.GetAppPoolNames().Any(a => string.Equals(a, appPoolName, StringComparison.OrdinalIgnoreCase));
            }

            public override void CreateWebSite(string name, string path, string appPool, bool https, BindingInfo binding)
            {
                using (var manager = new ServerManager())
                {
                    var site = manager.Sites.Add(name, https ? "https" : "http", binding.ToString(), path);
                    site.ApplicationDefaults.ApplicationPoolName = appPool;
                    manager.CommitChanges();
                }
            }

            /// <summary>
            /// Check if an IIS hosted website of name <paramref name="name"/> exists.
            /// </summary>
            /// <param name="name">The name of the website to look for in the IIS</param>
            /// <returns>True if website already exists else false</returns>
            public override bool WebSiteExists(string name)
            {
                using (var manager = new ServerManager())
                {
                    return manager.Sites.Any(site => site.Name.Equals(name));
                }
            }

            /// <summary>
            /// Verifies that the required IIS6 interfaces are present.
            /// </summary>
            private void ValidateIis7Management()
            {
                using (var manager = new ServerManager())
                {
                }
            }
        }
    }
}