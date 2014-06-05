using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public class MessageClient : IMessageClient
    {
        private IFramedClient framedClient;
        private StacksSerializationHandler packetSerializer;

        private IMessageIdCache messageIdCache;

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

        public MessageClient(IFramedClient framedClient,
                             IStacksSerializer packetSerializer,
                             IMessageHandler messageHandler,
                             Func<MessageIdRegistration, MessageIdRegistration> registration)
            : this(registration(new MessageIdRegistration()).CreateCache(), 
                   framedClient, packetSerializer, messageHandler)
        {
        }

        public MessageClient(IFramedClient framedClient, 
                             IStacksSerializer packetSerializer,
                             IMessageHandler messageHandler)
            : this(new MessageIdCache(),
                   framedClient, packetSerializer, messageHandler)
        {
        }

        private MessageClient(IMessageIdCache messageIdCache, 
                              IFramedClient framedClient,
                              IStacksSerializer packetSerializer,
                              IMessageHandler messageHandler)
        {

            this.messageIdCache = messageIdCache;

            this.framedClient = framedClient;
            this.packetSerializer = new StacksSerializationHandler(
                                            messageIdCache,
                                            this,
                                            packetSerializer,
                                            messageHandler);

            this.framedClient.Received.Subscribe(PacketReceived);
        }

        public IObservable<Unit> Connect(IPEndPoint endPoint)
        {
            return framedClient.Connect(endPoint);
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int messageId = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    this.packetSerializer.Deserialize(messageId, ms);
                }
            }
        }

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


        public void Close()
        {
            this.framedClient.Close();
        }

    }
}
