using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IRawSocketClient : IRawByteClient
    {
        Socket Socket { get; }
    }
}
