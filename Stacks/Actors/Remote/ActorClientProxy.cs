using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Tcp;

namespace Stacks.Actors
{
    using Stacks.Actors.Remote.CodeGen;

    public class ActorClientProxy
    {
        public static Task<IActorClientProxy<T>> CreateProxy<T>(IPEndPoint remoteEndPoint)
        {
            var proxyCreator = new ActorClientProxy();

            return proxyCreator.AuxCreate<T>(remoteEndPoint);
        }

        public static Task<IActorClientProxy<T>> CreateProxy<T>(string endPoint)
        {
            return CreateProxy<T>(IPHelpers.Parse(endPoint));
        }


        public static Task<IActorClientProxy> CreateProxy(Type actorType, IPEndPoint remoteEndPoint)
        {
            var proxyCreator = new ActorClientProxy();

            return (Task<IActorClientProxy>)proxyCreator
                        .GetType()
                        .GetMethod("AuxCreate", BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, new Type[] { typeof(IPEndPoint) }, null)
                        .MakeGenericMethod(actorType)
                        .Invoke(proxyCreator, new[] { remoteEndPoint });
        }

        public static Task<IActorClientProxy> CreateProxy(Type actorType, string remoteEndPoint)
        {
            return CreateProxy(actorType, IPHelpers.Parse(remoteEndPoint));
        }



        public static Task<T> CreateActor<T>(IPEndPoint remoteEndPoint)
        {
            return CreateProxy<T>(remoteEndPoint).ContinueWith(t =>
                {
                    if (t.Exception == null)
                        return t.Result.Actor;
                    else
                        throw t.Exception.InnerException;
                });
        }

        public static Task<T> CreateActor<T>(string remoteEndPoint)
        {
            return CreateActor<T>(IPHelpers.Parse(remoteEndPoint));
        }



        [Obsolete("This method is obsolete. Choose between CreateActor and CreateProxy.")]
        public static Task<T> Create<T>(IPEndPoint remoteEndPoint) { return CreateActor<T>(remoteEndPoint); }
        [Obsolete("This method is obsolete. Choose between CreateActor and CreateProxy.")]
        public static Task<T> Create<T>(string remoteEndPoint) { return CreateActor<T>(remoteEndPoint); }



        private ClientActorTypeBuilder tBuilder;

        private Task<IActorClientProxy<T>> AuxCreate<T>(IPEndPoint remoteEndPoint)
        {
            var actorType = typeof(T);
            Ensure.IsInterface(actorType, "actorType", "Only interfaces can be used to create actor client proxy");

            tBuilder = new ClientActorTypeBuilder("ActorClientProxy_ " + actorType.FullName);
            tBuilder.DefineMessagesFromInterfaceType(actorType);
            var actorImplType = tBuilder.CreateActorType(actorType);

            tBuilder.SaveToFile();

            var actor = Activator.CreateInstance(actorImplType, new[] { remoteEndPoint });
    
            return ((ActorClientProxyTemplate<T>)actor).Connect()
                        .ContinueWith(t =>
                            {
                                if (t.Exception == null)
                                    return (IActorClientProxy<T>)t.Result;
                                else
                                    throw t.Exception.InnerException;
                            });
        }
    }
}
