using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public class ReactiveMessageClient<T> : MessageClientBase
    {
        private Dictionary<int, Action<MemoryStream>> deserializeByMessageId;
        private ReactiveMessageReceiverCreator<T> messageReceiverCreator;


        public T Packets { get; private set; }


        public ReactiveMessageClient(IFramedClient framedClient,
                                     IStacksSerializer packetSerializer)
            : base(framedClient, new MessageIdCache(), packetSerializer)
        {
            Ensure.IsNotNull(framedClient, "framedClient");
            Ensure.IsNotNull(packetSerializer, "packetSerializer");

            this.messageReceiverCreator = new ReactiveMessageReceiverCreator<T>(base.messageIdCache, packetSerializer);
            
            this.Packets = this.messageReceiverCreator.CreateReceiverImplementation(out deserializeByMessageId);

            this.framedClient.Received.Subscribe(PacketReceived);
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            Action<MemoryStream> handler;
            
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int messageId = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    if (deserializeByMessageId.TryGetValue(messageId, out handler))
                    {
                        handler(ms);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            string.Format("No registered message handler for message id {0}", messageId));
                    }
                }
            }
        }

    }
}
