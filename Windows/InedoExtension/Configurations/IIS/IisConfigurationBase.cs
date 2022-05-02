using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Inedo.Diagnostics;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [Serializable]
    public abstract class IisConfigurationBase : PersistedConfiguration, IExistential
    {
        internal IisConfigurationBase()
        {
        }

        [Persistent]
        [ScriptAlias("Exists")]
        [DefaultValue(true)]
        public bool Exists { get; set; } = true;

        protected void SetPropertiesFromMwa(ILogSink logger, ConfigurationElement mwaConfig, IisConfigurationBase template = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (mwaConfig == null)
                throw new ArgumentNullException(nameof(mwaConfig));
            var config = this;

            var keyprops = from prop in Persistence.GetPersistentProperties(this.GetType(), false)
                           where Attribute.IsDefined(prop, typeof(ScriptAliasAttribute))
                              && Attribute.IsDefined(prop, typeof(ConfigurationKeyAttribute))
                           select prop;
            foreach (var keyProp in keyprops)
            {
                if (SkipTemplateProperty(null, keyProp))
                    continue;
                var mappedProperty = FindMatchingProperty(keyProp.Name.Split('_'), mwaConfig);
                if (mappedProperty != null)
                    keyProp.SetValue(config, mappedProperty.GetValue());
                else
                    logger.LogWarning($"Matching MWA property \"{keyProp.Name}\" was not found.");
            }
            if (template == null)
                return;

            var templateProperties = from prop in Persistence.GetPersistentProperties(this.GetType(), false)
                                     where Attribute.IsDefined(prop, typeof(ScriptAliasAttribute))
                                        && !Attribute.IsDefined(prop, typeof(ConfigurationKeyAttribute))
                                     select prop;
            foreach (var templateProperty in templateProperties)
            {
                if (SkipTemplateProperty(template, templateProperty))
                    continue;

                var mappedProperty = FindMatchingProperty(templateProperty.Name.Split('_'), mwaConfig);
                if (mappedProperty != null)
                    templateProperty.SetValue(config, mappedProperty.GetValue());
                else
                    logger.LogWarning($"Matching MWA property \"{templateProperty.Name}\" was not found.");
            }
        }

        protected void SetPropertiesOnMwa(ILogSink logger, ConfigurationElement pool)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            var config = this;

            var configProperties = Persistence.GetPersistentProperties(config.GetType(), false)
                .Where(p => Attribute.IsDefined(p, typeof(ScriptAliasAttribute)));

            foreach (var configProperty in configProperties)
            {
                if (SkipTemplateProperty(config, configProperty))
                    continue;

                object value = configProperty.GetValue(config);

                var mappedProperty = FindMatchingProperty(configProperty.Name.Split('_'), pool);
                if (mappedProperty != null)
                    mappedProperty.SetValue(value);
                else
                    logger.LogWarning($"Matching MWA property \"{configProperty.Name}\" was not found.");
            }
        }

        protected virtual bool SkipTemplateProperty(IisConfigurationBase template, PropertyInfo templateProperty)
        {
            if (templateProperty.Name == nameof(IExistential.Exists))
                return true;

            if (template == null)
                return false;

            var value = templateProperty.GetValue(template);
            if (value != null)
                return false;

            return true;
        }

        private static MappedProperty FindMatchingProperty(IList<string> propertyNames, object propertyInstance)
        {
            if (propertyNames.Count == 0)
                return null;

            string name = propertyNames.First();
            var mwaProperty = propertyInstance.GetType().GetProperty(name);
            if (mwaProperty == null)
                return null;

            var appPoolPropertyInstance = mwaProperty.GetValue(propertyInstance);

            return FindMatchingProperty(propertyNames.Skip(1).ToArray(), appPoolPropertyInstance)
                ?? new MappedProperty(propertyInstance, mwaProperty);
        }

        private class MappedProperty
        {
            public MappedProperty(object instance, PropertyInfo prop)
            {
                if (instance == null)
                    throw new ArgumentNullException(nameof(instance));
                if (prop == null)
                    throw new ArgumentNullException(nameof(prop));

                this.Instance = instance;
                this.MwaProperty = prop;
            }

            public object Instance { get; }
            public PropertyInfo MwaProperty { get; }

            public void SetValue(object value)
            {
                this.MwaProperty.SetValue(this.Instance, value);
            }

            public object GetValue()
            {
                return this.MwaProperty.GetValue(this.Instance);
            }
        }
    }
}
