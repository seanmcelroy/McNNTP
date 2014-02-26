using System.Net.Sockets;
using System.Threading;

namespace McNNTP.Server
{
    internal sealed class AcceptAsyncState
    {
        public ManualResetEvent AcceptComplete;
        public TcpListener Listener;
    }
}
