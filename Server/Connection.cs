using System.Text;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace McNNTP.Server
{
    // State object for reading client data asynchronously
    internal class Connection
    {
        // Client  socket.
        [CanBeNull]
        public Socket WorkSocket;
        [NotNull]
        public readonly object SendLock = new object();
        // Size of receive buffer.
        public const int BUFFER_SIZE = 1024;
        // Receive buffer.
        [NotNull]
        public readonly byte[] Buffer = new byte[BUFFER_SIZE];
        // Received data string.
        [NotNull]
        public readonly StringBuilder Builder = new StringBuilder();

        public bool CanPost { get; set; }

        [CanBeNull]
        public string CurrentNewsgroup { get; set; }
        public long? CurrentArticleNumber { get; set; }
    }
}
