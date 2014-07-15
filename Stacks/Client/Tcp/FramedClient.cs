using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Net;

namespace Stacks.Tcp
{
    public class FramedClient : IFramedClient
    {
        private IRawByteClient client;
        private ResizableCyclicBuffer recvBuffer;
        private Subject<ArraySegment<byte>> received;

        public IObservable<Unit> Connected
        {
            get { return client.Connected; }
        }

        public IObservable<Exception> Disconnected
        {
            get { return client.Disconnected; }
        }

        public IObservable<int> Sent
        {
            get { return client.Sent; }
        }

        public IObservable<ArraySegment<byte>> Received
        {
            get { return received; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return client.LocalEndPoint; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return client.RemoteEndPoint; }
        }

        public FramedClient(IRawByteClient client)
        {
            this.received = new Subject<ArraySegment<byte>>();
            this.client = client;
            this.recvBuffer = new ResizableCyclicBuffer(4096);

            this.client.Received.Subscribe(ClientReceivedData);
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

        public void Close()
        {
            this.client.Close();
        }

        private void OnReceived(ArraySegment<byte> data)
        {
            received.OnNext(data);
        }

        public IExecutor Executor
        {
            get { return client.Executor; }
        }

        public bool IsConnected
        {
            get { return client.IsConnected; }
        }

        public IObservable<Unit> Connect(System.Net.IPEndPoint remoteEndPoint)
        {
            return client.Connect(remoteEndPoint);
        }

        public IObservable<Unit> Connect(string endPoint)
        {
            return Connect(IPHelpers.Parse(endPoint));
        }
    }
}
