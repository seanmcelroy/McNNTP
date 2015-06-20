using System.Configuration;
using System.Xml;

namespace McNNTP.Core.Server.Configuration
{
    public abstract class ProtocolConfigurationElementBase : ConfigurationElement
    {
        protected internal void DeserializeElementForConfig(XmlReader reader, bool serializeCollectionKey)
        {
            this.DeserializeElement(reader, serializeCollectionKey);
        }
    }
}
