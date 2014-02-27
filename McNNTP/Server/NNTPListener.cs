using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;

namespace McNNTP.Server
{
    internal class NntpListener : TcpListener
    {
        // Thread signal.
        private readonly ManualResetEvent _allDone = new ManualResetEvent(false);

        private readonly NntpServer _server;

        public NntpListener([NotNull] NntpServer server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            _server = server;
        }

        public PortClass PortType { get; set; }

        public void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new NntpListener(_server, localEndPoint);

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
        
        private void AcceptCallback(IAsyncResult ar)
        {
            var acceptState = (AcceptAsyncState)ar.AsyncState;

            // Signal the main thread to continue.
            acceptState.AcceptComplete.Set();

            // Get the socket that handles the client request.
            var listener = acceptState.Listener;
            var handler = listener.EndAcceptTcpClient(ar);
            //Thread.CurrentThread.Name = string.Format("{0}:{1}", ((IPEndPoint)handler.RemoteEndPoint).Address, ((IPEndPoint)handler.RemoteEndPoint).Port);

            // Create the state object.
            var connection = new Connection(handler, _server._sessionProvider, _server.ServerPath, _server.AllowStartTLS, _server.AllowPosting, _server.ShowBytes, _server.ShowCommands, _server.ShowData);

            //var sslStream = new SslStream(state.Stream);
            //sslStream.AuthenticateAsServer();
            //state.Stream = sslStream;

            _server._connections.Add(connection);

            connection.Process();
        }
    }
}
