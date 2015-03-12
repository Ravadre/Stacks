using System;
using System.Net;
using Stacks.Actors.Remote.CodeGen;

// ReSharper disable InconsistentNaming

namespace Stacks.Actors
{
    public class ActorServerProxy
    {
        private static ServerActorTypeBuilder tBuilder;

        public static IActorServerProxy Create<I, T>(IPEndPoint bindEndPoint)
            where T : class, I, new()
        {
            return Create(ActorSystem.Default.CreateActor<I, T>(() => new T()), bindEndPoint,
                ActorServerProxyOptions.Default);
        }

        public static IActorServerProxy Create<I, T>(string bindEndPoint)
            where T : class, I, new()
        {
            return Create(ActorSystem.Default.CreateActor<I, T>(() => new T()), IPHelpers.Parse(bindEndPoint),
                ActorServerProxyOptions.Default);
        }

        public static IActorServerProxy Create<I, T>(IPEndPoint bindEndPoint, ActorServerProxyOptions options)
            where T : class, I, new()
        {
            return Create(ActorSystem.Default.CreateActor<I, T>(() => new T()), bindEndPoint, options);
        }

        public static IActorServerProxy Create<I, T>(string bindEndPoint, ActorServerProxyOptions options)
            where T : class, I, new()
        {
            return Create(ActorSystem.Default.CreateActor<I, T>(() => new T()), IPHelpers.Parse(bindEndPoint), options);
        }

        public static IActorServerProxy Create<I>(IPEndPoint bindEndPoint, I actorImpl)
        {
            return Create(actorImpl, bindEndPoint, ActorServerProxyOptions.Default);
        }

        public static IActorServerProxy Create<I>(string bindEndPoint, I actorImpl)
        {
            return Create(actorImpl, IPHelpers.Parse(bindEndPoint), ActorServerProxyOptions.Default);
        }

        public static IActorServerProxy Create<I>(IPEndPoint bindEndPoint, I actorImpl, ActorServerProxyOptions options)
        {
            return Create(actorImpl, bindEndPoint, options);
        }

        public static IActorServerProxy Create<I>(string bindEndPoint, I actorImpl, ActorServerProxyOptions options)
        {
            return Create(actorImpl, IPHelpers.Parse(bindEndPoint), options);
        }

        private static IActorServerProxy Create<I>(I actorImplementation, IPEndPoint bindEndPoint,
            ActorServerProxyOptions options)
        {
            var aType = actorImplementation.GetType();
            tBuilder = new ServerActorTypeBuilder("ActorServerProxy_ " + aType.FullName);

            tBuilder.DefineMessagesFromInterfaceType(aType);

            var actorImplType = tBuilder.CreateActorType(aType);
            tBuilder.SaveToFile();

            var actor = Activator.CreateInstance(actorImplType, actorImplementation, bindEndPoint, options);
            return (IActorServerProxy)actor;
        }
    }
}