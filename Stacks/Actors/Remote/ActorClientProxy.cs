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
        public static T Create<T>(IPEndPoint remoteEndPoint)
        {
            var type = typeof(T);
            return (T)Create(type, remoteEndPoint);
        }

        public static T Create<T>(string endPoint)
        {
            return Create<T>(IPHelpers.Parse(endPoint));
        }

        public static object Create(Type actorType, IPEndPoint remoteEndPoint)
        {
            var proxyCreator = new ActorClientProxy();

            return proxyCreator.AuxCreate(actorType, remoteEndPoint);
        }

        public static object Create(Type actorType, string endPoint)
        {
            return Create(actorType, IPHelpers.Parse(endPoint));
        }


        private Type actorType;
        private Type actorImplType;
      
        private ClientActorTypeBuilder tBuilder;


        private object AuxCreate(Type actorType, IPEndPoint remoteEndPoint)
        {
           this.actorType = actorType;

            Ensure.IsInterface(actorType, "actorType", "Only interfaces can be used to create actor client proxy");

            tBuilder = new ClientActorTypeBuilder("ActorClientProxy_ " + actorType.FullName);
            tBuilder.DefineMessagesFromInterfaceType(actorType);
            this.actorImplType = tBuilder.CreateActorType(actorType);

            tBuilder.SaveToFile();

            return Activator.CreateInstance(actorImplType, new[] { remoteEndPoint });
        }


    }
}
