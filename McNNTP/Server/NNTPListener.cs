using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace McNNTP.Server
{
    internal class NntpListener : TcpListener
    {
        // Thread signal.
        private readonly ManualResetEvent _allDone = new ManualResetEvent(false);

        public NntpListener(IPEndPoint localEp)
            : base(localEp)
        {
        }
        public AsyncCallback AcceptCallback { get; set; }

        public PortClass PortType { get; set; }

        public void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new NntpListener(localEndPoint);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    _allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    listener.BeginAcceptTcpClient(AcceptCallback, new AcceptAsyncState { Listener = listener, AcceptComplete = _allDone});

                    // Wait until a connection is made before continuing.
                    _allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
