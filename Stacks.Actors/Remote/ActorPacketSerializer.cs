using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors.Proto;

namespace Stacks.Actors.Remote
{
    public class ActorPacketSerializer
    {
        private readonly IStacksSerializer defaultSerializer;

        public ActorPacketSerializer(IStacksSerializer defaultSerializer)
        {
            this.defaultSerializer = defaultSerializer;
        }

        public virtual void Serialize<T>(ActorProtocolFlags packetFlags, string requestName,  T packet, MemoryStream ms)
        {
            defaultSerializer.Serialize(packet, ms);
        }

        public virtual T Deserialize<T>(ActorProtocolFlags packetFlags, string requestName, MemoryStream ms)
        {
            return defaultSerializer.Deserialize<T>(ms);
        }
    }
}
