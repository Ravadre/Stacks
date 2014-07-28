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
        public static Task<T> Create<T>(IPEndPoint remoteEndPoint)
        {
            var type = typeof(T);
            return Create(type, remoteEndPoint).ContinueWith(t =>
                {
                    if (t.Exception == null)
                        return (T)(object)t.Result;
                    else
                        throw t.Exception.InnerException;
                });
        }

        public static Task<T> Create<T>(string endPoint)
        {
            return Create<T>(IPHelpers.Parse(endPoint));
        }

        public static Task<ActorClientProxyTemplate> Create(Type actorType, IPEndPoint remoteEndPoint)
        {
            var proxyCreator = new ActorClientProxy();

            return proxyCreator.AuxCreate(actorType, remoteEndPoint);
        }

        public static Task<ActorClientProxyTemplate> Create(Type actorType, string endPoint)
        {
            return Create(actorType, IPHelpers.Parse(endPoint));
        }

        private ClientActorTypeBuilder tBuilder;

        private Task<ActorClientProxyTemplate> AuxCreate(Type actorType, IPEndPoint remoteEndPoint)
        {
            Ensure.IsInterface(actorType, "actorType", "Only interfaces can be used to create actor client proxy");

            tBuilder = new ClientActorTypeBuilder("ActorClientProxy_ " + actorType.FullName);
            tBuilder.DefineMessagesFromInterfaceType(actorType);
            var actorImplType = tBuilder.CreateActorType(actorType);

            //tBuilder.SaveToFile();

            var actor = Activator.CreateInstance(actorImplType, new[] { remoteEndPoint });
            return ((ActorClientProxyTemplate)actor).Connect();
        }
    }
}
