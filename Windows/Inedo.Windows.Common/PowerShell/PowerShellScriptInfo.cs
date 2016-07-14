using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Inedo.Extensions.Windows.PowerShell
{
    /// <summary>
    /// Contains metadata about a script.
    /// </summary>
    [Serializable]
    public sealed partial class PowerShellScriptInfo : IEquatable<PowerShellScriptInfo>, IComparable<PowerShellScriptInfo>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellScriptInfo"/> class.
        /// </summary>
        /// <param name="name">The name of the script.</param>
        /// <param name="description">The description of the script.</param>
        /// <param name="parameters">The parameters for the script.</param>
        public PowerShellScriptInfo(string name = null, string description = null, IEnumerable<PowerShellParameterInfo> parameters = null)
        {
            this.Name = name;
            this.Description = description;

            if (parameters != null)
                this.Parameters = Array.AsReadOnly(parameters.Distinct().ToArray());
        }

        /// <summary>
        /// Gets the script name.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the script description if it is available; otherwise null.
        /// </summary>
        public string Description { get; private set; }
        /// <summary>
        /// Gets the script parameters if they are available; otherwise null.
        /// </summary>
        public ReadOnlyCollection<PowerShellParameterInfo> Parameters { get; private set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format(
                "{0}({1})",
                this.Name ?? "??",
                this.Parameters == null ? "??" : string.Join(", ", this.Parameters)
            );
        }
        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other" /> parameter; otherwise false.
        /// </returns>
        public bool Equals(PowerShellScriptInfo other)
        {
            if (object.ReferenceEquals(this, other))
                return true;
            if (object.ReferenceEquals(other, null))
                return false;

            return StringComparer.OrdinalIgnoreCase.Equals(this.Name, other.Name);
        }
        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        /// True if the specified <see cref="System.Object" /> is equal to this instance; otherwise false.
        /// </returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as PowerShellScriptInfo);
        }
        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name);
        }
        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared.
        /// </returns>
        public int CompareTo(PowerShellScriptInfo other)
        {
            if (object.ReferenceEquals(this, other))
                return 0;
            if (object.ReferenceEquals(other, null))
                return 1;

            return StringComparer.OrdinalIgnoreCase.Compare(this.Name, other.Name);
        }
    }
}
