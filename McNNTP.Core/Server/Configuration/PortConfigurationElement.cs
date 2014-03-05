using System;
using System.Configuration;

namespace McNNTP.Core.Server.Configuration
{
    public class PortConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// The port number
        /// </summary>
        [ConfigurationProperty("number", IsRequired = true)]
        public int Port
        {
            get { return (int)this["number"]; }
            set { this["number"] = value; }
        }

        /// <summary>
        /// Configuration information about NewRelic
        /// </summary>
        [ConfigurationProperty("ssl", IsRequired = false)]
        public string Ssl
        {
            get
            {
                return (string)this["ssl"];
            }
            set
            {
                PortClass portType;
                if (!Enum.TryParse(value, true, out portType))
                    throw new ConfigurationErrorsException(string.Format("ssl property value '{0}' is not a valid port type", value));
                
                this["ssl"] = portType.ToString();
            }
        }
    }
}
