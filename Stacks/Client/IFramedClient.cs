using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IFramedClient : ISocketClient
    {
        IObservable<int> Sent { get; }
        IObservable<ArraySegment<byte>> Received { get; }

        void SendPacket(byte[] packet);
        void SendPacket(ArraySegment<byte> packet);
        void SendPacket(FramedClientBuffer packet);
        FramedClientBuffer PreparePacketBuffer(int packetBytes);
    }
}
