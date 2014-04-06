using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Client
{
    public class FramedClient : IFramedClient
    {
        private IRawByteClient client;
        private ResizableCyclicBuffer recvBuffer;

        public event Action<Exception> Disconnected
        {
            add { this.client.Disconnected += value; }
            remove { this.client.Disconnected -= value; }
        }

        public event Action<int> Sent
        {
            add { this.client.Sent += value; }
            remove { this.client.Sent -= value; }
        }

        public event Action<ArraySegment<byte>> Received;

        public FramedClient(IRawByteClient client)
        {
            this.client = client;
            this.recvBuffer = new ResizableCyclicBuffer(4096);

            this.client.Received += ClientReceivedData;
        }

        private void ClientReceivedData(ArraySegment<byte> data)
        {
            recvBuffer.AddData(data);

            foreach (var packet in recvBuffer.GetPackets())
            {
                OnReceived(packet);
            }
        }

        public void SendPacket(byte[] packet)
        {
            Ensure.IsNotNull(packet, "packet");

            SendPacket(new ArraySegment<byte>(packet));
        }

        public void SendPacket(ArraySegment<byte> packet)
        {
            Ensure.IsNotNull(packet.Array, "packet.Array");

            var buffer = FramedClientBuffer.FromPacket(packet);
            SendPacket(buffer);
        }

        public void SendPacket(FramedClientBuffer packet)
        {
            this.client.Send(packet.InternalBuffer);
        }

        public FramedClientBuffer PreparePacketBuffer(int packetBytes)
        {
            return new FramedClientBuffer(packetBytes);
        }

        private void OnReceived(ArraySegment<byte> data)
        {
            var h = Received;
            if (h != null)
            {
                try { h(data); }
                catch { }
            }
        }
    }
}
