using System.IO;
using System.Text;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace McNNTP.Server
{
    // State object for reading client data asynchronously
    internal class Connection
    {
        // Client  socket.
        [CanBeNull] public TcpClient Client;
        [CanBeNull] public Stream Stream;

        [NotNull] public readonly object SendLock = new object();
        // Size of receive buffer.
        public const int BUFFER_SIZE = 1024;
        // Receive buffer.
        [NotNull] public readonly byte[] Buffer = new byte[BUFFER_SIZE];
        // Received data string.
        [NotNull] public readonly StringBuilder Builder = new StringBuilder();

        public bool CanPost { get; set; }

        #region Administrator functions

        [CanBeNull]
        public string CanApproveGroups { get; set; }

        public bool CanCancel { get; set; }
        public bool CanCreateGroup { get; set; }
        public bool CanDeleteGroup { get; set; }
        public bool CanCheckGroups { get; set; }

        /// <summary>
        /// Indicates the connection can operate as a server, such as usiing the IHAVE command
        /// </summary>
        public bool CanInject { get; set; }

        #endregion

        #region Authentication
        [CanBeNull]
        public string Username { get; set; }
        public bool Authenticated { get; set; }
        public bool TLS { get; set; }
        #endregion

        [CanBeNull]
        public string CurrentNewsgroup { get; set; }
        public long? CurrentArticleNumber { get; set; }
    }
}
