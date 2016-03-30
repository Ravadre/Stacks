using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public abstract class MessageClientBase : IMessageClient
    {
        protected readonly IFramedClient framedClient;
        protected readonly IMessageIdCache messageIdCache;
        protected readonly IStacksSerializer packetSerializer;


        public IExecutor Executor => framedClient.Executor; 
        public bool IsConnected => framedClient.IsConnected;

        public IObservable<Unit> Connected => framedClient.Connected;
        public IObservable<Exception> Disconnected => framedClient.Disconnected;
        public IObservable<int> Sent => framedClient.Sent;

        public IPEndPoint LocalEndPoint => framedClient.LocalEndPoint;
        public IPEndPoint RemoteEndPoint => framedClient.RemoteEndPoint;

        protected MessageClientBase(IFramedClient client, IMessageIdCache messageIdCache,
                                 IStacksSerializer packetSerializer)
        {
            this.framedClient = client;
            this.messageIdCache = messageIdCache;
            this.packetSerializer = packetSerializer;
        }

        public IObservable<Unit> Connect(IPEndPoint endPoint) => framedClient.Connect(endPoint);
        public IObservable<Unit> Connect(string endPoint) => Connect(IPHelpers.Parse(endPoint));

        public void Close() => framedClient.Close();

        public unsafe void Send<T>(T obj)
        {
            var messageId = messageIdCache.GetMessageId<T>();

            using (var ms = new MemoryStream())
            {
                ms.SetLength(4);
                ms.Position = 4;
                this.packetSerializer.Serialize(obj, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    int* iBuf = (int*)buf;
                    *iBuf = messageId;
                }

                this.framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        public void PreLoadTypesFromAssemblyOfType<T>()
        {
            messageIdCache.PreLoadTypesFromAssemblyOfType<T>();
        }

        public void PreLoadType<T>()
        {
            messageIdCache.PreLoadType<T>();
        }

        public void PreLoadType(Type type)
        {
            messageIdCache.PreLoadType(type);
        } 
    }
}
