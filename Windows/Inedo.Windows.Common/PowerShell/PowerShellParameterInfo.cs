using System;

namespace Inedo.Extensions.Windows.PowerShell
{
    /// <summary>
    /// Contains metadata about a script parameter.
    /// </summary>
    [Serializable]
    public sealed class PowerShellParameterInfo : IEquatable<PowerShellParameterInfo>, IComparable<PowerShellParameterInfo>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellParameterInfo"/> class.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="description">The description of the parameter.</param>
        /// <param name="defaultValue">The default value of the parameter.</param>
        /// <param name="isBooleanOrSwitch">The input type code most appropriate for the parameter. See <see cref="Domains.ScriptParameterTypes"/> for valid values.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="isBooleanOrSwitch"/> is not one of the values in <see cref="Domains.ScriptParameterTypes"/>.</exception>
        public PowerShellParameterInfo(string name, string description = null, string defaultValue = null, bool isBooleanOrSwitch = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            
            this.Name = name;
            this.Description = description;
            this.DefaultValue = defaultValue;
            this.IsBooleanOrSwitch = isBooleanOrSwitch;
        }

        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the parameter description if it is available; otherwise null.
        /// </summary>
        public string Description { get; private set; }
        /// <summary>
        /// Gets the default value of the parameter if it is available; otherwise null.
        /// </summary>
        public string DefaultValue { get; private set; }
        /// <summary>
        /// Gets whether the parameter is a boolean or switch
        /// </summary>
        public bool IsBooleanOrSwitch { get; private set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.Name;
        }
        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other" /> parameter; otherwise false.
        /// </returns>
        public bool Equals(PowerShellParameterInfo other)
        {
            if (object.ReferenceEquals(this, other))
                return true;
            if (object.ReferenceEquals(other, null))
                return false;

            return string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
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
            return this.Equals(obj as PowerShellParameterInfo);
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
        public int CompareTo(PowerShellParameterInfo other)
        {
            if (object.ReferenceEquals(this, other))
                return 0;
            if (object.ReferenceEquals(other, null))
                return 1;

            return StringComparer.OrdinalIgnoreCase.Compare(this.Name, other.Name);
        }
    }
}
