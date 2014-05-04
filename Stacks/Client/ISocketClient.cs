using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface ISocketClient
    {
        IExecutor Executor { get; }
        bool IsConnected { get; }

        event Action Connected;

        Task Connect(IPEndPoint remoteEndPoint);
    }
}
