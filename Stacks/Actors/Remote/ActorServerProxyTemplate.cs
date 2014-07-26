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
    public abstract class ActorServerProxyTemplate<T>
    {
        protected SocketServer server;
        protected List<FramedClient> clients;
        protected T actorImplementation;
        protected Dictionary<string, Action<FramedClient, long, MemoryStream>> handlers;
        protected IStacksSerializer serializer;

        public ActorServerProxyTemplate(T actorImplementation, IPEndPoint bindEndPoint)
        {
            this.serializer = new ProtoBufStacksSerializer();
            this.clients = new List<FramedClient>();
            this.handlers = new Dictionary<string, Action<FramedClient, long, MemoryStream>>();
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
                        fixed (byte* b = bs.Array)
                        {
                            byte* s = b + bs.Offset;
                            long reqId = *(long*)s;
                            int headerLen = *(int*)(s + 8);
                            string msgName = new string((sbyte*)s, 12, headerLen);
                            int pOffset = 12 + headerLen;

                            using (var ms = new MemoryStream(bs.Array, bs.Offset + pOffset, bs.Count - 12 - headerLen))
                            {
                                HandleMessage(client, reqId, msgName, ms);
                            }

                        }
                    }
                });

            clients.Add(client);
        }

        private void HandleMessage(FramedClient client, long reqId, string messageName, MemoryStream ms)
        {
            Action<FramedClient, long, MemoryStream> handler;

            if (!handlers.TryGetValue(messageName, out handler))
            {
                throw new Exception(
                    string.Format("Client {0} sent message for method {1}, which has no handler registered",
                    client.RemoteEndPoint, messageName));
            }

            handler(client, reqId, ms);
        }

        protected void HandleResponse<R>(FramedClient client, long reqId, Task<R> actorResponse, IReplyMessage<R> msgToSend)
        {
            if (actorResponse == null)
            {
                Send(client, reqId, msgToSend);
            }
            else
            {
                actorResponse.ContinueWith(t =>
                {
                    try
                    {
                        msgToSend.SetResult(t.Result);
                    }
                    catch (Exception exc)
                    {
                        msgToSend.SetError(exc.Message);
                    }

                    Send(client, reqId, msgToSend);
                });
            }
        }

        private unsafe void Send<R>(FramedClient client, long requestId, IReplyMessage<R> packet)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(8);
                ms.Position = 8;
                serializer.Serialize<IReplyMessage<R>>(packet, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(long*)buf = requestId;
                }

                client.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }
    }
}
