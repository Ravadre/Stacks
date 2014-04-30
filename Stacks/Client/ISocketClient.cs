using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface ISocketClient : IRawByteClient
    {
        IExecutor Executor { get; }
        bool IsConnected { get; }

        event Action Connected;

        void Connect(IPEndPoint remoteEndPoint);
    }
}
