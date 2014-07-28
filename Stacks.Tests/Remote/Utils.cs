using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;

namespace Stacks.Tests.Remote
{
    public static class Utils
    {
        public static void CreateServerAndClient<T, I>(Func<T> fac, out IActorServerProxy server, out I client)
        {
            server = ActorServerProxy.Create("tcp://*:0", fac);
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.Create<I>("tcp://localhost:" + port).Result;
        }

        public static void CreateServerAndClient<T, I>(out IActorServerProxy server, out I client)
            where T : new()
        {
            server = ActorServerProxy.Create<T>("tcp://*:0");
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.Create<I>("tcp://localhost:" + port).Result;
        }
    }
}
