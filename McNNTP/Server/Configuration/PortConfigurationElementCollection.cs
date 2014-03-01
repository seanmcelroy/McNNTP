using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace McNNTP.Server.Configuration
{
    public class PortConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<PortConfigurationElement>
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new PortConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (PortConfigurationElement) element;
            return pce.Port.ToString(CultureInfo.InvariantCulture) + pce.Ssl;
        }

        public PortConfigurationElement this[int index]
        {
            get { return (PortConfigurationElement) BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemove(index);
                BaseAdd(index, value);
            }
        }

        public void Add(PortConfigurationElement serviceConfig)
        {
            BaseAdd(serviceConfig);
        }

        public void Clear()
        {
            BaseClear();
        }

        public void Remove(PortConfigurationElement serviceConfig)
        {
            BaseRemove(serviceConfig.Port.ToString(CultureInfo.InvariantCulture) + serviceConfig.Ssl);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Remove(string name)
        {
            BaseRemove(name);
        }

        public new IEnumerator<PortConfigurationElement> GetEnumerator()
        {
            return BaseGetAllKeys().Select(key => (PortConfigurationElement)BaseGet(key)).GetEnumerator();
        }
    }
}
