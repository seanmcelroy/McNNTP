namespace McNNTP.Core.Server.Configuration
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.Linq;

    public class PortConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<PortConfigurationElement>
    {
        /// <inheritdoc/>
        protected override ConfigurationElement CreateNewElement()
        {
            return new PortConfigurationElement();
        }

        /// <inheritdoc/>
        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (PortConfigurationElement)element;
            return pce.Port.ToString(CultureInfo.InvariantCulture) + pce.Ssl;
        }

        public PortConfigurationElement this[int index]
        {
            get => (PortConfigurationElement)this.BaseGet(index);

            set
            {
                if (this.BaseGet(index) != null)
                {
                    this.BaseRemove(index);
                }

                this.BaseAdd(index, value);
            }
        }

        public void Add(PortConfigurationElement serviceConfig)
        {
            this.BaseAdd(serviceConfig);
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public void Remove(PortConfigurationElement serviceConfig)
        {
            this.BaseRemove(serviceConfig.Port.ToString(CultureInfo.InvariantCulture) + serviceConfig.Ssl);
        }

        public void RemoveAt(int index)
        {
            this.BaseRemoveAt(index);
        }

        public void Remove(string name)
        {
            this.BaseRemove(name);
        }

        /// <inheritdoc/>
        public new IEnumerator<PortConfigurationElement> GetEnumerator()
        {
            return this.BaseGetAllKeys().Select(key => (PortConfigurationElement)this.BaseGet(key)).GetEnumerator();
        }
    }
}
