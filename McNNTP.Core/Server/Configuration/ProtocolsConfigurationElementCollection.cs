namespace McNNTP.Core.Server.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Xml;

    using McNNTP.Core.Server.Configuration.IRC;

    public class ProtocolsConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<ProtocolConfigurationElementBase>
    {
        /// <inheritdoc/>
        protected override ConfigurationElement CreateNewElement()
        {
            return new IrcProtocolConfigurationElement();
        }

        /// <inheritdoc/>
        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (ProtocolConfigurationElementBase) element;
            return pce.GetType().Name;
        }

        public ProtocolConfigurationElementBase this[int index]
        {
            get
            {
                return (ProtocolConfigurationElementBase)this.BaseGet(index);
            }

            set
            {

                if (this.BaseGet(index) != null)
                {
                    this.BaseRemove(index);
                }

                this.BaseAdd(index, value);
            }

        }

        public void Add(ProtocolConfigurationElementBase protocolConfig)
        {
            this.BaseAdd(protocolConfig);
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public void Remove(ProtocolConfigurationElementBase protocolConfig)
        {
            this.BaseRemove(protocolConfig.GetType().Name);
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
        public new IEnumerator<ProtocolConfigurationElementBase> GetEnumerator()
        {
            return this.BaseGetAllKeys().Select(key => (ProtocolConfigurationElementBase)this.BaseGet(key)).GetEnumerator();
        }

        /// <inheritdoc/>
        protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
        {
            if (elementName.StartsWith("irc", StringComparison.OrdinalIgnoreCase))
            {
                var element = new IrcProtocolConfigurationElement();
                element.DeserializeElementForConfig(reader, false);
                this.BaseAdd(element);
                return true;
            }

            return false;
        }
    }
}
