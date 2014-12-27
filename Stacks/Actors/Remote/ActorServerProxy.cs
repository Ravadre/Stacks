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

        public static IActorServerProxy Create<T>(IPEndPoint bindEndPoint, ActorServerProxyOptions options)
            where T : new()
        {
            return Create(bindEndPoint, new T(), options);
        }

        public static IActorServerProxy Create<T>(string bindEndPoint, ActorServerProxyOptions options)
            where T : new()
        {
            return Create<T>(IPHelpers.Parse(bindEndPoint), new T(), options);
        }

        public static IActorServerProxy Create<T>(IPEndPoint bindEndPoint, T actorImpl)
        {
            return Create(actorImpl, bindEndPoint, ActorServerProxyOptions.Default);
        }

        public static IActorServerProxy Create<T>(string bindEndPoint, T actorImpl)
        {
            return Create(actorImpl, IPHelpers.Parse(bindEndPoint), ActorServerProxyOptions.Default);
        }

        public static IActorServerProxy Create<T>(IPEndPoint bindEndPoint, T actorImpl, ActorServerProxyOptions options)
        {
            return Create(actorImpl, bindEndPoint, options);
        }

        public static IActorServerProxy Create<T>(string bindEndPoint, T actorImpl, ActorServerProxyOptions options)
        {
            return Create(actorImpl, IPHelpers.Parse(bindEndPoint), options);
        }

        private static ServerActorTypeBuilder tBuilder;

        private static IActorServerProxy Create<T>(T actorImplementation, IPEndPoint bindEndPoint, ActorServerProxyOptions options)
        {
            var aType = actorImplementation.GetType();
            tBuilder = new ServerActorTypeBuilder("ActorServerProxy_ " + aType.FullName);

            tBuilder.DefineMessagesFromInterfaceType(aType);

            var actorImplType = tBuilder.CreateActorType(aType);
            tBuilder.SaveToFile();

            var actor = Activator.CreateInstance(actorImplType, new object[] { actorImplementation, bindEndPoint, options });
            return (ActorServerProxyTemplate<T>)actor;
        }
    }
}
