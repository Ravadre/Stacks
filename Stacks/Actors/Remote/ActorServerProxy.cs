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

namespace Stacks.Actors
{
    public class ActorServerProxy
    {
        public static IActorServerProxy Create<T>(IPEndPoint bindEndPoint)
            where T: new()
        {
            return Create(bindEndPoint, new T());
        }

        public static IActorServerProxy Create<T>(string bindEndPoint)
            where T: new()
        {
            return Create<T>(IPHelpers.Parse(bindEndPoint), new T());
        }

        public static IActorServerProxy Create<T>(IPEndPoint bindEndPoint, T actorImpl)
        {
            return Create(actorImpl, bindEndPoint);
        }

        public static IActorServerProxy Create<T>(string bindEndPoint, T actorImpl)
        {
            return Create(actorImpl, IPHelpers.Parse(bindEndPoint));
        }

        private static ServerActorTypeBuilder tBuilder;

        private static IActorServerProxy Create<T>(T actorImplementation, IPEndPoint bindEndPoint)
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
