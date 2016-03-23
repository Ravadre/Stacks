using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.Remote
{
    public class ActorClientProxyOptions
    {
        public Func<IStacksSerializer, ActorPacketSerializer> SerializerProvider { get; private set; }

        public ActorClientProxyOptions(Func<IStacksSerializer, ActorPacketSerializer> serializerProvider = null)
        {
            SerializerProvider = serializerProvider;
        }


        public static readonly ActorClientProxyOptions Default =
            new ActorClientProxyOptions(serializerProvider: null);
    }
}
