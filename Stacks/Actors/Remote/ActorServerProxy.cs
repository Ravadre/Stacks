using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors.Remote.CodeGen;
using System.Reactive.Threading.Tasks;
using Stacks.Tcp;
using System.Reactive.Linq;

namespace Stacks.Actors.Remote
{
    public class ActorServerProxy
    {
        public static ActorServerProxyTemplate<T> Create<T>(IPEndPoint bindEndPoint)
            where T: new()
        {
            return Create(bindEndPoint, () => new T());
        }

        public static ActorServerProxyTemplate<T> Create<T>(string bindEndPoint)
            where T: new()
        {
            return Create<T>(IPHelpers.Parse(bindEndPoint), () => new T());
        }

        public static ActorServerProxyTemplate<T> Create<T>(IPEndPoint bindEndPoint, Func<T> factory)
        {
            return Create(factory(), bindEndPoint);
        }

        public static ActorServerProxyTemplate<T> Create<T>(string bindEndPoint, Func<T> factory)
        {
            return Create(factory(), IPHelpers.Parse(bindEndPoint));
        }

        private static ServerActorTypeBuilder tBuilder;

        private static ActorServerProxyTemplate<T> Create<T>(T actorImplementation, IPEndPoint bindEndPoint)
        {
            var aType = actorImplementation.GetType();
            tBuilder = new ServerActorTypeBuilder("ActorServerProxy_ " + aType.FullName);

            tBuilder.DefineMessagesFromInterfaceType(aType);

            var actorImplType = tBuilder.CreateActorType(aType);
            tBuilder.SaveToFile();

            var actor = Activator.CreateInstance(actorImplType, new object[] { actorImplementation, bindEndPoint });
            return (ActorServerProxyTemplate<T>)actor;
        }
    }
}
