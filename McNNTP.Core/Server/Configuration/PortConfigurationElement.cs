// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortConfigurationElement.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A configuration element that specifies a TCP port on which the process will listen for incoming requests
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.Configuration
{
    using System;
    using System.Configuration;

    using JetBrains.Annotations;

    /// <summary>
    /// A configuration element that specifies a TCP port on which the process will listen for incoming requests
    /// </summary>
    public class PortConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the port number
        /// </summary>
        [ConfigurationProperty("number", IsRequired = true)]
        public int Port
        {
            get { return (int)this["number"]; }
            [UsedImplicitly]
            set { this["number"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating if and how secure sockets are implemented on this listening port
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">Thrown when the value passed for the SSL type is not a valid port SSL option</exception>
        [ConfigurationProperty("ssl", IsRequired = false)]
        public string Ssl
        {
            get
            {
                return (string)this["ssl"];
            }

            [UsedImplicitly]
            set
            {
                PortClass portType;
                bool parsed;

                try
                {
                    parsed = Enum.TryParse(value, true, out portType);
                }
                catch (ArgumentException ae)
                {
                    throw new ConfigurationErrorsException("ssl property value is not a valid ssl type", ae);
                }

                if (!parsed)
                {
                    string message;
                    try
                    {
                        message = string.Format("ssl property value '{0}' is not a valid ssl type", value);
                    }
                    catch (ArgumentNullException ane)
                    {
                        throw new ConfigurationErrorsException("ssl property value is not a valid ssl type", ane);
                    }
                    catch (FormatException fe)
                    {
                        throw new ConfigurationErrorsException("ssl property value is not a valid ssl type", fe);
                    }

                    throw new ConfigurationErrorsException(message);
                }
                
                this["ssl"] = portType.ToString();
            }
        }
    }
}
