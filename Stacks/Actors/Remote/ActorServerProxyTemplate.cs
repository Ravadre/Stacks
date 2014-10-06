﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public abstract class ActorServerProxyTemplate<T> : IActorServerProxy
    {
        protected IExecutor executor;
        protected SocketServer server;
        protected List<FramedClient> clients;
        protected T actorImplementation;
        protected Dictionary<string, Action<FramedClient, long, MemoryStream>> handlers;
        protected Dictionary<string, object> obsHandlers;
        protected IStacksSerializer serializer;

        protected Dictionary<int, Action<FramedClient, MemoryStream>> protocolHandlers;

        public IPEndPoint BindEndPoint { get { return server.BindEndPoint; } }

        public ActorServerProxyTemplate(T actorImplementation, IPEndPoint bindEndPoint)
        {
            this.executor = new ActionBlockExecutor();
            this.serializer = new ProtoBufStacksSerializer();
            this.clients = new List<FramedClient>();
            this.handlers = new Dictionary<string, Action<FramedClient, long, MemoryStream>>();
            this.obsHandlers = new Dictionary<string, object>();
            this.actorImplementation = actorImplementation;

            protocolHandlers = new Dictionary<int, Action<FramedClient, MemoryStream>>();
            protocolHandlers[Proto.ActorProtocol.HandshakeId] = HandleHandshakeMessage;
            protocolHandlers[Proto.ActorProtocol.PingId] = HandlePingMessage;

            server = new SocketServer(executor, bindEndPoint);
            server.Connected.Subscribe(ClientConnected);
            server.Start();
        }

        public void Stop()
        {
            server.Stop();
            executor.Enqueue(() =>
                {
                    foreach (var client in clients)
                    {
                        client.Close();
                    }
                });
        }

        private void HandleClientReceivedData(FramedClient client, ArraySegment<byte> bs)
        {
            unsafe
            {
                fixed (byte* b = bs.Array)
                {
                    byte* s = b + bs.Offset;
                    Proto.ActorProtocolFlags header = *(Proto.ActorProtocolFlags*)s;

                    if (header == Proto.ActorProtocolFlags.StacksProtocol)
                    {
                        HandleProtocolMessage(client, bs, s);
                    }
                    else
                    {
                        if (header != Proto.ActorProtocolFlags.RequestReponse)
                            throw new Exception("Invalid actor protocol header. Expected request-response");

                        bs = HandleRequestMessage(client, bs, s);
                    }
                }
            }
        }

        private unsafe ArraySegment<byte> HandleRequestMessage(FramedClient client, ArraySegment<byte> bs, byte* s)
        {
            long reqId = *(long*)(s + 4);
            int msgNameLength = *(int*)(s + 12);
            string msgName = new string((sbyte*)s, 16, msgNameLength);
            int pOffset = 16 + msgNameLength;

            using (var ms = new MemoryStream(bs.Array, bs.Offset + pOffset, bs.Count - pOffset))
            {
                HandleMessage(client, reqId, msgName, ms);
            }
            return bs;
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
                    try
                    {
                        HandleClientReceivedData(client, bs);
                    }
                    catch
                    {
                        client.Close();
                    }
                });

            clients.Add(client);
        }

        private unsafe void HandleProtocolMessage(FramedClient client, ArraySegment<byte> bs, byte* s)
        {
            int pid = *(int*)(s + 4);

            Action<FramedClient, MemoryStream> handler;
            bool hasHandler = protocolHandlers.TryGetValue(pid, out handler);

            if (!hasHandler)
                throw new Exception("Invalid actor protocol header.");

            using (var ms = new MemoryStream(bs.Array, bs.Offset + 8, bs.Count - 8))
            {
                handler(client, ms);
            }
        }

        private unsafe void HandleHandshakeMessage(FramedClient client, MemoryStream ms)
        {
            var req = serializer.Deserialize<Proto.HandshakeRequest>(ms);

            AuxSendProtocolPacket(client, Proto.ActorProtocol.HandshakeId,
                  new Proto.HandshakeResponse
                    {
                        RequestedProtocolVersion = req.ClientProtocolVersion,
                        ServerProtocolVersion = Proto.ActorProtocol.Version,
                        ProtocolMatch = req.ClientProtocolVersion == Proto.ActorProtocol.Version
                    });
        }

        private void HandlePingMessage(FramedClient client, MemoryStream ms)
        {
            var req = serializer.Deserialize<Proto.Ping>(ms);

            AuxSendProtocolPacket(client, Proto.ActorProtocol.PingId,
                  new Proto.Ping
                  {
                      Timestamp = req.Timestamp
                  });
        }


        private void HandleMessage(FramedClient client, long reqId, string messageName, MemoryStream ms)
        {
            Action<FramedClient, long, MemoryStream> handler;

            if (!handlers.TryGetValue(messageName, out handler))
            {
                throw new Exception(
                    string.Format(
                        "Client {0} sent message for method {1}, which has no handler registered",
                        client.RemoteEndPoint,
                        messageName));

            }

            handler(client, reqId, ms);
        }

        protected void HandleResponseNoResult(FramedClient client, long reqId, Task actorResponse, IReplyMessage<Unit> msgToSend)
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
                        t.Wait();
                        msgToSend.SetResult(Unit.Default);
                    }
                    catch (AggregateException exc)
                    {
                        msgToSend.SetError(exc.InnerException.Message);
                    }
                    catch (Exception exc)
                    {
                        msgToSend.SetError(exc.Message);
                    }

                    Send(client, reqId, msgToSend);
                });
            }
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
                    catch (AggregateException exc)
                    {
                        msgToSend.SetError(exc.InnerException.Message);
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
                ms.SetLength(12);
                ms.Position = 12;
                serializer.Serialize<IReplyMessage<R>>(packet, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(Proto.ActorProtocolFlags*)buf = Proto.ActorProtocolFlags.RequestReponse;
                    *(long*)(buf + 4) = requestId;
                }

                client.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        protected unsafe void SendObs<M>(string name, M msg)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(8);
                ms.Position = 8;
                var nameBytes = Encoding.ASCII.GetBytes(name);
                ms.Write(nameBytes, 0, nameBytes.Length);
                this.serializer.Serialize(msg, ms);

                ms.Position = 0;
                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(Proto.ActorProtocolFlags*)buf = Proto.ActorProtocolFlags.Observable;
                    *(int*)(buf + 4) = nameBytes.Length;
                }

                var packet = new ArraySegment<byte>(buffer, 0, (int)ms.Length);
                executor.Enqueue(() =>
                    {
                        foreach (var c in clients)
                        {
                            c.SendPacket(packet);
                        }
                    });
            }
        }

        private unsafe void AuxSendProtocolPacket<P>(FramedClient client, int packetId, P packet)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(8);
                ms.Position = 8;
                serializer.Serialize(packet, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(Proto.ActorProtocolFlags*)buf = Proto.ActorProtocolFlags.StacksProtocol;
                    *(int*)(buf + 4) = packetId;
                }

                client.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }
    }
}
