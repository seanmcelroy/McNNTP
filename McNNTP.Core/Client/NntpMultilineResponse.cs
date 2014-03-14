using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McNNTP.Core.Client
{
    internal class NntpMultilineResponse : NntpResponse
    {
        internal IEnumerable<string> Lines { get; private set; }

        internal NntpMultilineResponse(int code, string message, IEnumerable<string> lines)
            : base(code, message)
        {
            Lines = lines;
        }
    }
}
