using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace Stacks.Actors.Remote
{
    public abstract class ActorServerProxyTemplate
    {   
        protected SocketServer server;
        protected List<FramedClient> clients;
        protected object actorImplementation;
        protected Dictionary<string, Action<MemoryStream>> handlers;

        public ActorServerProxyTemplate(object actorImplementation, IPEndPoint bindEndPoint)
        {
            this.clients = new List<FramedClient>();
            this.handlers = new Dictionary<string, Action<MemoryStream>>();
            this.actorImplementation = actorImplementation;

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
                    unsafe
                    {
                        fixed(byte* b = bs.Array)
                        {
                            byte* s = b + bs.Offset;
                            long reqId = *(long*)s;
                            int headerLen = *(int*)(s + 8);
                            string msgName = new string((sbyte*)s, 12, headerLen);
                            int pOffset = 12 + headerLen;

                            using (var ms = new MemoryStream(bs.Array, pOffset, bs.Count - 12 - headerLen))
                            {
                                HandleMessage(msgName, ms);
                            }

                        }
                    }
                });

            clients.Add(client);
        }

        private void HandleMessage(string messageName, MemoryStream ms)
        {
            try
            {
                HandleMessageAux(messageName, ms);
            }
            catch (Exception exc)
            { }
        }

        protected abstract void HandleMessageAux(string msgName, MemoryStream ms);
    }
}
