using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;
using ProtoBuf;
using System.Reactive;
using System.Net;

namespace Stacks.Actors
{
    class ActorRemoteMessageClient
    {  
        protected readonly IFramedClient framedClient;
        protected readonly IStacksSerializer packetSerializer;

        public IExecutor Executor
        {
            get { return framedClient.Executor; }
        }

        public bool IsConnected
        {
            get { return framedClient.IsConnected; }
        }

        public IObservable<Unit> Connected
        {
            get { return this.framedClient.Connected; }
        }

        public IObservable<Exception> Disconnected
        {
            get { return this.framedClient.Disconnected; }
        }

        public IObservable<int> Sent
        {
            get { return this.framedClient.Sent; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return framedClient.LocalEndPoint; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return framedClient.RemoteEndPoint; }
        }

        public IObservable<Unit> Connect(IPEndPoint endPoint)
        {
            return framedClient.Connect(endPoint);
        }

        public event Action<long, MemoryStream> MessageReceived;

        public ActorRemoteMessageClient(IFramedClient client)
        {
            this.framedClient = client;
            this.packetSerializer = new ProtoBufStacksSerializer();

            this.framedClient.Received.Subscribe(PacketReceived);
        }



        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int header = *(int*)b;

                if (Bit.IsSet(header, (int)ActorProtocolFlags.RequestReponse))
                {
                    long requestId = *(long*)(b + 4);

                    using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 12, buffer.Count - 12))
                    {
                        OnMessageReceived(requestId, ms);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void OnMessageReceived(long requestId, MemoryStream ms)
        {
            var h = MessageReceived;
            if (h != null)
                h(requestId, ms);
        }

        public unsafe void Send<T>(string msgName, long requestId, T obj)
        {
            var msgNameBytes = Encoding.ASCII.GetBytes(msgName);

            using (var ms = new MemoryStream())
            {
                ms.SetLength(16);
                ms.Position = 16;
                ms.Write(msgNameBytes, 0, msgNameBytes.Length);
                this.packetSerializer.Serialize(obj, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(ActorProtocolFlags*)buf = ActorProtocolFlags.RequestReponse;
                    *(long*)(buf + 4) = requestId;
                    *(int*)(buf + 12) = msgNameBytes.Length;
                }

                this.framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        public void Close()
        {
            this.framedClient.Close();
        }
    }
}
