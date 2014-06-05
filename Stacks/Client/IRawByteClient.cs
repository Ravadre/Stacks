using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IRawByteClient : ISocketClient
    {
        event Action<ArraySegment<byte>> Received;
        IObservable<int> Sent { get; }
        IObservable<Exception> Disconnected { get; }

        void Send(byte[] buffer);
        void Send(ArraySegment<byte> buffer);

        void Close();
    }
}
