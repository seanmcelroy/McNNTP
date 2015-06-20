using System.Configuration;
using System.Xml;

namespace McNNTP.Core.Server.Configuration
{
    public abstract class UserDirectoryConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// The port number
        /// </summary>
        [ConfigurationProperty("priority", IsRequired = true)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        protected internal void DeserializeElementForConfig(XmlReader reader, bool serializeCollectionKey)
        {
            this.DeserializeElement(reader, serializeCollectionKey);
        }
    }
}
