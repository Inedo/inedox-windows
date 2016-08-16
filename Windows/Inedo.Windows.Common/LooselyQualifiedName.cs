using System;
using System.Linq;

namespace Inedo.Extensions.Windows
{
    /// <summary>
    /// Represents a name that may be qualified with a namespace with relaxed namespace restrictions.
    /// </summary>
    public sealed class LooselyQualifiedName : IEquatable<LooselyQualifiedName>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LooselyQualifiedName"/> class.
        /// </summary>
        /// <param name="name">The local name.</param>
        /// <param name="namespaceName">The namespace.</param>
        public LooselyQualifiedName(string name, string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            this.Name = name;
            if (!string.IsNullOrWhiteSpace(namespaceName))
                this.Namespace = namespaceName;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="LooselyQualifiedName"/> class.
        /// </summary>
        /// <param name="name">The local name.</param>
        public LooselyQualifiedName(string name)
            : this(name, null)
        {
        }

        public static bool operator ==(LooselyQualifiedName name1, LooselyQualifiedName name2) => Equals(name1, name2);
        public static bool operator !=(LooselyQualifiedName name1, LooselyQualifiedName name2) => !Equals(name1, name2);

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace { get; }
        /// <summary>
        /// Gets the local name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Gets the fully qualified name.
        /// </summary>
        public string FullName
        {
            get
            {
                if (this.Namespace == null)
                    return this.Name;
                else
                    return this.Namespace + "::" + this.Name;
            }
        }

        public static LooselyQualifiedName Parse(string s)
        {
            Exception error;
            var name = TryParseInternal(s, out error);
            if (error != null)
                throw error;
            else
                return name;
        }
        public static LooselyQualifiedName TryParse(string s)
        {
            Exception error;
            return TryParseInternal(s, out error);
        }
        public static bool Equals(LooselyQualifiedName name1, LooselyQualifiedName name2)
        {
            if (ReferenceEquals(name1, name2))
                return true;
            if (ReferenceEquals(name1, null) || ReferenceEquals(name2, null))
                return false;

            return string.Equals(name1.Name, name2.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(name1.Namespace, name2.Namespace, StringComparison.OrdinalIgnoreCase);
        }
        public static bool Equals(LooselyQualifiedName name1, string name2)
        {
            if (ReferenceEquals(name1, null) && ReferenceEquals(name2, null))
                return true;
            if (ReferenceEquals(name1, null) || ReferenceEquals(name2, null))
                return false;

            return Equals(name1, TryParse(name2));
        }

        public override string ToString() => this.FullName;
        public bool Equals(LooselyQualifiedName other) => Equals(this, other);
        public override bool Equals(object obj) => Equals(this, obj as LooselyQualifiedName);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name?.Replace('_', '-'));

        private static LooselyQualifiedName TryParseInternal(string s, out Exception error)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                error = new ArgumentNullException(nameof(s));
                return null;
            }

            var parts = s.Split(new[] { "::" }, 2, StringSplitOptions.None);
            var name = parts.Last().Trim();
            var @namespace = parts.Length > 1 ? parts[0].Trim() : null;

            error = null;
            return new LooselyQualifiedName(name, @namespace);
        }
    }
}
