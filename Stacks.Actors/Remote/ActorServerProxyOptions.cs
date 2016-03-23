using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors.Remote;

namespace Stacks.Actors
{
    public class ActorServerProxyOptions
    {
        public bool ActorSessionInjectionEnabled { get; private set; }
        public Func<IStacksSerializer, ActorPacketSerializer> SerializerProvider { get; private set; }

        public ActorServerProxyOptions(bool actorSessionInjectionEnabled, Func<IStacksSerializer, ActorPacketSerializer> serializerProvider = null)
        {
            SerializerProvider = serializerProvider;
            ActorSessionInjectionEnabled = actorSessionInjectionEnabled;
        }

        public static readonly ActorServerProxyOptions Default = 
            new ActorServerProxyOptions(actorSessionInjectionEnabled: false,
                                        serializerProvider: null);
    }
}
