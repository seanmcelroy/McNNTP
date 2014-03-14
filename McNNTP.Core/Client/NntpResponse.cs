using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McNNTP.Core.Client
{
    internal class NntpResponse
    {
        public int Code { get; private set; }
        
        public string Message { get; private set; }

        internal NntpResponse(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
