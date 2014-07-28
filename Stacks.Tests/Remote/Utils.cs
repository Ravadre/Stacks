using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;

namespace Stacks.Tests.Remote
{
    public static class Utils
    {
        public static void CreateServerAndClient<T, I>(T impl, out IActorServerProxy server, out I client)
        {
            server = ActorServerProxy.Create("tcp://*:0", impl);
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
