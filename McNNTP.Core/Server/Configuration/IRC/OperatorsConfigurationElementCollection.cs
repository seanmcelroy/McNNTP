namespace McNNTP.Core.Server.Configuration.IRC
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Xml;

    using JetBrains.Annotations;

    /// <summary>
    /// A collection of <see cref="OperatorConfigurationElement"/> configuration elements that provide
    /// O: lines, or the oper block, enumerating allowed administrators
    /// </summary>
    [ConfigurationCollection(typeof(OperatorConfigurationElement), AddItemName = "operator"), UsedImplicitly]
    public class OperatorsConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<OperatorConfigurationElement>
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new OperatorConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (OperatorConfigurationElement) element;
            return pce.GetType().Name;
        }

        public OperatorConfigurationElement this[int index]
        {
            get { return (OperatorConfigurationElement)this.BaseGet(index); }
            set
            {
                if (this.BaseGet(index) != null)
                    this.BaseRemove(index);
                this.BaseAdd(index, value);
            }
        }

        public void Add(OperatorConfigurationElement protocolConfig)
        {
            this.BaseAdd(protocolConfig);
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public void Remove(OperatorConfigurationElement protocolConfig)
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

        public new IEnumerator<OperatorConfigurationElement> GetEnumerator()
        {
            return this.BaseGetAllKeys().Select(key => (OperatorConfigurationElement)this.BaseGet(key)).GetEnumerator();
        }
    }
}
