namespace McNNTP.Core.Server.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Xml;

    public class UserDirectoryConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<UserDirectoryConfigurationElement>
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new LdapDirectoryConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (UserDirectoryConfigurationElement) element;
            return pce.GetType().Name;
        }

        public UserDirectoryConfigurationElement this[int index]
        {
            get
            {
                return (UserDirectoryConfigurationElement)this.BaseGet(index);
            }

            set
            {
                if (this.BaseGet(index) != null)
                    this.BaseRemove(index);
                this.BaseAdd(index, value);
            }
        }

        public void Add(UserDirectoryConfigurationElement serviceConfig)
        {
            this.BaseAdd(serviceConfig);
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public void Remove(UserDirectoryConfigurationElement serviceConfig)
        {
            this.BaseRemove(serviceConfig.GetType().Name);
        }

        public void RemoveAt(int index)
        {
            this.BaseRemoveAt(index);
        }

        public void Remove(string name)
        {
            this.BaseRemove(name);
        }

        public new IEnumerator<UserDirectoryConfigurationElement> GetEnumerator()
        {
            return this.BaseGetAllKeys().Select(key => (UserDirectoryConfigurationElement)this.BaseGet(key)).GetEnumerator();
        }

        protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
        {
            if (elementName.StartsWith("ldap", StringComparison.OrdinalIgnoreCase))
            {
                var element = new LdapDirectoryConfigurationElement();
                element.DeserializeElementForConfig(reader, false);
                this.BaseAdd(element);
                return true;
            }

            if (elementName.StartsWith("local", StringComparison.OrdinalIgnoreCase))
            {
                var element = new LocalDirectoryConfigurationElement();
                element.DeserializeElementForConfig(reader, false);
                this.BaseAdd(element);
                return true;
            }

            return false;
        }
    }
}
