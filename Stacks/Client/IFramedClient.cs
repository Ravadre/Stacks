using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IFramedClient : ISocketClient
    {
        IObservable<Exception> Disconnected { get; }
        IObservable<int> Sent { get; }
        event Action<ArraySegment<byte>> Received;

        void SendPacket(byte[] packet);
        void SendPacket(ArraySegment<byte> packet);
        void SendPacket(FramedClientBuffer packet);
        FramedClientBuffer PreparePacketBuffer(int packetBytes);

        void Close();
    }
}
