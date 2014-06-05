using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IRawByteClient : ISocketClient
    {
        IObservable<ArraySegment<byte>> Received { get; }
        IObservable<int> Sent { get; }
        IObservable<Exception> Disconnected { get; }

        void Send(byte[] buffer);
        void Send(ArraySegment<byte> buffer);

        void Close();
    }
}
