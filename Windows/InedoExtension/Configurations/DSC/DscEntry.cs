using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.ExecutionEngine;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.Configurations.DSC
{
    /// <summary>
    /// Represents serializable arbitrary configuration that can be translated to
    /// and from <see cref="RuntimeValue"/> instances.
    /// </summary>
    /// <remarks>
    /// This class is only intended to be used with <see cref="DscConfiguration"/>,
    /// and is written in such a way as to be compatible with the serialization used
    /// by <see cref="Persistence"/>.
    /// </remarks>
    [Serializable]
    [SlimSerializable]
    public sealed class DscEntry
    {
        [Persistent]
        public string Key { get; set; }
        [Persistent]
        public string Text { get; set; }
        [Persistent]
        public IEnumerable<DscEntry> List { get; set; }
        [Persistent]
        public IEnumerable<DscEntry> Map { get; set; }

        public RuntimeValue ToRuntimeValue()
        {
            if (this.Text != null)
                return this.Text;

            if (this.List != null)
                return new RuntimeValue(this.List.Select(e => e.ToRuntimeValue()).ToList());

            if (this.Map != null)
            {
                var d = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in this.Map)
                {
                    if (!string.IsNullOrEmpty(item.Key))
                        d[item.Key] = item.ToRuntimeValue();
                }

                return new RuntimeValue(d);
            }

            return default;
        }
    }
}
