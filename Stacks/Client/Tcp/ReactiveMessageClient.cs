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
    public class ReactiveMessageClient<T> : MessageClientBase
    {
        public T Packets { get; private set; }

        public ReactiveMessageClient(IFramedClient framedClient,
                                     IStacksSerializer packetSerializer)
            : base(framedClient, new MessageIdCache(), packetSerializer)
        {
            this.framedClient.Received.Subscribe(PacketReceived);
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int messageId = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    //this.packetSerializationHandler.Deserialize(messageId, ms);
                }
            }
        }
    }
}
