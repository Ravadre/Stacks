using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Client
{
    public interface IFramedClient
    {
        event Action<Exception> Disconnected;
        event Action<int> Sent;
        event Action<ArraySegment<byte>> Received;

        void SendPacket(byte[] packet);
        void SendPacket(ArraySegment<byte> packet);
        void SendPacket(FramedClientBuffer packet);
        FramedClientBuffer PreparePacketBuffer(int packetBytes);

        void Close();
    }
}
