namespace McNNTP.Core.Server.Configuration
{
    using System.Configuration;
    using System.Xml;

    public abstract class UserDirectoryConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the port number.
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
