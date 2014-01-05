using System.Text;
using System.Net.Sockets;

namespace McNNTP.Server
{
    // State object for reading client data asynchronously
    internal class Connection
    {
        // Client  socket.
        public Socket WorkSocket = null;
        public object SendLock = new object();
        // Size of receive buffer.
        public const int BUFFER_SIZE = 1024;
        // Receive buffer.
        public byte[] Buffer = new byte[BUFFER_SIZE];
        // Received data string.
        public StringBuilder sb = new StringBuilder();

        public bool CanPost { get; set; }

        public string CurrentNewsgroup { get; set; }
        public long? CurrentArticleNumber { get; set; }
    }
}
