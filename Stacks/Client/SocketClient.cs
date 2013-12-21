using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Stacks.Executors;

namespace Stacks
{
    public class SocketClient
    {
        private IExecutor executor;

        private Socket socket;

        public SocketClient(IExecutor executor, Socket socket)
        {
            this.executor = executor;

            this.socket = socket;
        }
    }
}
