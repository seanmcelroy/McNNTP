// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMessage.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   A message is a single communication posted to a catalog.  Posting a message to a catalog may mean it is further redistributed or transmitted
//   to a remote system.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace mcnntp.common
{
    using System;
    using System.Collections.Generic;
    
    /// <summary>
    /// A message is a single communication posted to a catalog.  Posting a message to a catalog may mean it is further redistributed or transmitted
    /// to a remote system.
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets the unique identifier for the message within a catalog
        /// </summary>
        /// <remarks>This field must be unique</remarks>
        string Id { get; }

        /// <summary>
        /// Gets a dictionary of headers, keyed by the header name, and containing a tuple of just the header value, and the raw header line that includes the full header
        /// </summary>
        Dictionary<string, Tuple<string, string>> Headers { get; }

        /// <summary>
        /// Gets the raw header block for this message
        /// </summary>
        string HeaderRaw { get; }

        /// <summary>
        /// Gets the body of the message
        /// </summary>
        string Body { get; }
    }
}
