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
        public static ActorServerProxy Create<T>(IPEndPoint bindEndPoint)
        {
            return Create(typeof(T), bindEndPoint);
        }

        public static ActorServerProxy Create<T>(string bindEndPoint)
        {
            return Create<T>(IPHelpers.Parse(bindEndPoint));
        }

        private static ServerActorTypeBuilder typeBuilder;

        public static ActorServerProxy Create(Type actorType, IPEndPoint bindEndPoint)
        {
            Ensure.IsClass(actorType, "actorType");

            typeBuilder = new ServerActorTypeBuilder("ActorServerProxy_ " + actorType.FullName);

            typeBuilder.DefineMessagesFromInterfaceType(actorType);

            typeBuilder.SaveToFile();

            return new ActorServerProxy(bindEndPoint);
        }

        protected SocketServer server;
        protected List<FramedClient> clients;

        protected ActorServerProxy(IPEndPoint bindEndPoint)
        {
            clients = new List<FramedClient>();

            server = new SocketServer(bindEndPoint);
            server.Connected.Subscribe(ClientConnected);
            server.Start();
        }

        private void ClientConnected(SocketClient socketClient)
        {
            var client = new FramedClient(socketClient);

            client.Disconnected.Subscribe(exn =>
                {
                    clients.Remove(client);
                });

            client.Received.Subscribe(bs =>
                {

                });

            clients.Add(client);
        }
    }
}
