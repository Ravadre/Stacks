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

        public event Action<string, long, MemoryStream> MessageReceived;

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
                long requestId = *((long*)b);
                int headerSize = *((int*)(b + 8));
                var msgName = new string((sbyte*)(b + 12), 0, headerSize);

                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 12 + headerSize, buffer.Count - 12 - headerSize))
                {
                    OnMessageReceived(msgName, requestId, ms);
                }
            }
        }

        private void OnMessageReceived(string msgName, long requestId, MemoryStream ms)
        {
            var h = MessageReceived;
            if (h != null)
                h(msgName, requestId, ms);
        }

        public unsafe void Send<T>(string msgName, long requestId, T obj)
        {
            var msgNameBytes = Encoding.ASCII.GetBytes(msgName);

            using (var ms = new MemoryStream())
            {
                ms.SetLength(12);
                ms.Position = 12;
                ms.Write(msgNameBytes, 0, msgNameBytes.Length);
                this.packetSerializer.Serialize(obj, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(long*)buf = requestId;
                    *(int*)(buf + 8) = msgNameBytes.Length;
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
