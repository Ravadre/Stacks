using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Stacks.Actors.Remote;

namespace Stacks.Tests.Remote
{
    public static class Utils
    {
        public static void CreateServerAndClient<I>(I impl, out IActorServerProxy server, out I client)
        {
            server = ActorServerProxy.Create("tcp://*:0", impl);
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.CreateActor<I>("tcp://localhost:" + port).Result;
        }

        public static void CreateServerAndClient<I, T>(out IActorServerProxy server, out I client)
            where T : Actor, I, new()
        {
            CreateServerAndClient<I, T>(ActorServerProxyOptions.Default, out server, out client);
        }

        public static void CreateServerAndClient<I, T>(ActorServerProxyOptions options, out IActorServerProxy server, out I client)
            where T: Actor, I, new()
        {
            server = ActorServerProxy.Create<I, T>("tcp://*:0", options);
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.CreateActor<I>("tcp://localhost:" + port).Result;
        }

        public static void CreateServerAndClient<I, T>(ActorServerProxyOptions options, ActorClientProxyOptions cOptions,
            out IActorServerProxy server, out I client)
          where T : Actor, I, new()
        {
            server = ActorServerProxy.Create<I, T>("tcp://*:0", options);
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.CreateActor<I>("tcp://localhost:" + port, cOptions).Result;
        }


        public static void CreateServerAndClient<I>(I impl, out IActorServerProxy server, out IActorClientProxy<I> client)
        {
            server = ActorServerProxy.Create("tcp://*:0", impl);
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.CreateProxy<I>("tcp://localhost:" + port).Result;
        }

        public static void CreateServerAndClientProxy<I, T>(out IActorServerProxy server, out IActorClientProxy<I> client)
         where T : Actor, I, new()
        {
            server = ActorServerProxy.Create<I, T>("tcp://*:0");
            int port = server.BindEndPoint.Port;

            client = ActorClientProxy.CreateProxy<I>("tcp://localhost:" + port).Result;
        }

        public static int FindFreePort()
        {
            TcpListener l = new TcpListener(new IPEndPoint(IPAddress.Any, 0));
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
