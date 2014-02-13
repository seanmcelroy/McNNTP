using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace McNNTP.Server
{
    internal sealed class SendAsyncState
    {
        public Socket Socket;
        public string Payload;
    }
}
