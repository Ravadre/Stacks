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
    public class MessageClient : MessageClientBase
    {
        private StacksSerializationHandler packetSerializationHandler;

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
            : base(framedClient, messageIdCache, packetSerializer)
        {

            this.packetSerializationHandler = new StacksSerializationHandler(
                                            messageIdCache,
                                            this,
                                            packetSerializer,
                                            messageHandler);

            this.framedClient.Received.Subscribe(PacketReceived);
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int messageId = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    this.packetSerializationHandler.Deserialize(messageId, ms);
                }
            }
        }
    }
}
