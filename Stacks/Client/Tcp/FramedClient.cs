using System;
using System.Net;
using System.Reactive;
using System.Reactive.Subjects;

namespace Stacks.Tcp
{
    public class FramedClient : IFramedClient
    {
        private readonly IRawByteClient client;
        private readonly Subject<ArraySegment<byte>> received;
        private readonly ResizableCyclicBuffer recvBuffer;

        public FramedClient(IRawByteClient client)
        {
            received = new Subject<ArraySegment<byte>>();
            this.client = client;
            recvBuffer = new ResizableCyclicBuffer(4096);

            this.client.Received.Subscribe(ClientReceivedData);
        }

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
            packet = OnBeforeSendPacket(packet);
            client.Send(packet.InternalBuffer);
        }

        public FramedClientBuffer PreparePacketBuffer(int packetBytes)
        {
            return new FramedClientBuffer(packetBytes);
        }

        public void Close()
        {
            client.Close();
        }

        public IExecutor Executor
        {
            get { return client.Executor; }
        }

        public bool IsConnected
        {
            get { return client.IsConnected; }
        }

        public IObservable<Unit> Connect(IPEndPoint remoteEndPoint)
        {
            return client.Connect(remoteEndPoint);
        }

        public IObservable<Unit> Connect(string endPoint)
        {
            return Connect(IPHelpers.Parse(endPoint));
        }

        private void ClientReceivedData(ArraySegment<byte> data)
        {
            recvBuffer.AddData(data);

            foreach (var packet in recvBuffer.GetPackets())
            {
                OnReceived(packet);
            }
        }

        private void OnReceived(ArraySegment<byte> data)
        {
            data = OnBeforeReceivePacket(data);
            received.OnNext(data);
        }

        protected virtual ArraySegment<byte> OnBeforeReceivePacket(ArraySegment<byte> data)
        {
            return data;
        }

        protected virtual FramedClientBuffer OnBeforeSendPacket(FramedClientBuffer packet)
        {
            return packet;
        }

    }
}