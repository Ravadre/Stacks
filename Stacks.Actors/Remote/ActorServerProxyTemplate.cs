﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Actors.Proto;
using Stacks.Actors.Remote;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public abstract class ActorServerProxyTemplate<T> : IActorServerProxy
    {
        protected T actorImplementation;
        protected readonly ActorServerProxyOptions options;
        protected Dictionary<IFramedClient, IActorSession> actorSessions;
        protected Dictionary<FramedClient, Exception> clientErrors;
        protected List<FramedClient> clients;
        protected Dictionary<FramedClient, TimeSpan> clientTimestamps;
        protected IExecutor executor;
        protected Dictionary<string, Action<FramedClient, ActorProtocolFlags, string, long, MemoryStream>> handlers;
        protected bool isStopped;
        protected Dictionary<string, object> obsHandlers;
        protected Timer pingTimer;
        protected Dictionary<int, Action<FramedClient, ActorProtocolFlags, MemoryStream>> protocolHandlers;
        protected ActorPacketSerializer serializer;
        protected SocketServer server;
        private readonly Subject<IActorSession> clientActorConnected;
        private readonly Subject<ClientActorDisconnectedData> clientActorDisconnected;

        // ReSharper disable once PublicConstructorInAbstractClass
        public ActorServerProxyTemplate(T actorImplementation, IPEndPoint bindEndPoint, ActorServerProxyOptions options)
        {
            isStopped = false;
            executor = new ActionBlockExecutor();
            serializer = options.SerializerProvider == null ? new ActorPacketSerializer(new ProtoBufStacksSerializer()) : 
                options.SerializerProvider(new ProtoBufStacksSerializer());
            clients = new List<FramedClient>();
            handlers = new Dictionary<string, Action<FramedClient, ActorProtocolFlags, string, long, MemoryStream>>();
            actorSessions = new Dictionary<IFramedClient, IActorSession>();
            obsHandlers = new Dictionary<string, object>();
            this.actorImplementation = actorImplementation;
            this.options = options;

            clientActorConnected = new Subject<IActorSession>();
            clientActorDisconnected = new Subject<ClientActorDisconnectedData>();

            clientTimestamps = new Dictionary<FramedClient, TimeSpan>();
            clientErrors = new Dictionary<FramedClient, Exception>();
            pingTimer = new Timer(OnPingTimer, null, 10000, 10000);

            protocolHandlers = new Dictionary<int, Action<FramedClient, ActorProtocolFlags, MemoryStream>>
            {
                [ActorProtocol.HandshakeId] = HandleHandshakeMessage,
                [ActorProtocol.PingId] = HandlePingMessage
            };

            server = new SocketServer(executor, bindEndPoint);
            server.Connected.Subscribe(ClientConnected);
            server.Start();
        }

        public IPEndPoint BindEndPoint => server.BindEndPoint;

        public IObservable<IActorSession> ActorClientConnected => clientActorConnected.AsObservable();
        public IObservable<ClientActorDisconnectedData> ActorClientDisconnected => clientActorDisconnected.AsObservable();

        public Task<IActorSession[]> GetCurrentClientSessions()
        {
            return executor.PostTask(() => actorSessions.Values.ToArray());
        }

        public void Stop()
        {
            server.Stop();
            executor.Enqueue(() =>
            {
                isStopped = true;
                pingTimer.Change(Timeout.Infinite, Timeout.Infinite);

                foreach (var client in clients)
                {
                    client.Close();
                }
            });
        }

        private void OnPingTimer(object _)
        {
            executor.Enqueue(() =>
            {
                if (isStopped)
                    return;

                var now = TimeSpan.FromMilliseconds(Environment.TickCount);
                var oneMinute = TimeSpan.FromMinutes(1.0);

                foreach (var kv in clientTimestamps)
                {
                    var c = kv.Key;
                    var ts = kv.Value;

                    // Make sure that if tick count leaps we're ok.
                    if (now <= ts)
                    {
                        continue;
                    }

                    if (now - ts > oneMinute)
                    {
                        c.Close();
                    }
                }
            });
        }

        private void HandleClientReceivedData(FramedClient client, ArraySegment<byte> bs)
        {
            unsafe
            {
                fixed (byte* b = bs.Array)
                {
                    var s = b + bs.Offset;
                    var header = *(ActorProtocolFlags*) s;

                    if (header == ActorProtocolFlags.StacksProtocol)
                    {
                        HandleProtocolMessage(client, header, bs, s);
                    }
                    else
                    {
                        if (header != ActorProtocolFlags.RequestReponse)
                            throw new Exception("Invalid actor protocol header. Expected request-response");

                        HandleRequestMessage(client, header, bs, s);
                    }
                }
            }
        }

        private unsafe void HandleRequestMessage(FramedClient client, ActorProtocolFlags flags, ArraySegment<byte> bs, byte* s)
        {
            var reqId = *(long*) (s + 4);
            var msgNameLength = *(int*) (s + 12);
            var msgName = new string((sbyte*) s, 16, msgNameLength);
            var pOffset = 16 + msgNameLength;

            using (var ms = new MemoryStream(bs.Array, bs.Offset + pOffset, bs.Count - pOffset, true, true))
            {
                HandleMessage(client, flags, reqId, msgName, ms);
            }
        }

        private void ClientConnected(SocketClient socketClient)
        {
            if (isStopped)
                return;

            var client = new FramedClient(socketClient);

            client.Disconnected.Subscribe(exn =>
            {
                Exception clientError;
                clientErrors.TryGetValue(client, out clientError);

                clients.Remove(client);
                clientTimestamps.Remove(client);
                clientErrors.Remove(client);

                IActorSession isession;
                if (actorSessions.TryGetValue(client, out isession))
                {
                    actorSessions.Remove(client);
                    try
                    {
                        clientActorDisconnected.OnNext(
                            new ClientActorDisconnectedData(
                                isession,
                                clientError ?? exn));
                    }
                    catch
                    {
                        // TODO: Ignored for now
                    }
                }
            });

            client.Received.Subscribe(bs =>
            {
                try
                {
                    HandleClientReceivedData(client, bs);
                }
                catch (Exception exn)
                {
                    clientErrors[client] = exn;
                    client.Close();
                }
            });

            clients.Add(client);
            clientTimestamps[client] = TimeSpan.FromMilliseconds(Environment.TickCount);

            var session = new ActorSession(client);
            actorSessions[client] = session;
            try
            {
                clientActorConnected.OnNext(session);
            }
            catch
            {
                //TODO: Ignored for now
            }
        }

        private unsafe void HandleProtocolMessage(FramedClient client, ActorProtocolFlags flags, ArraySegment<byte> bs, byte* s)
        {
            var pid = *(int*) (s + 4);

            Action<FramedClient, ActorProtocolFlags, MemoryStream> handler;
            var hasHandler = protocolHandlers.TryGetValue(pid, out handler);

            if (!hasHandler)
                throw new Exception("Invalid actor protocol header.");

            using (var ms = new MemoryStream(bs.Array, bs.Offset + 8, bs.Count - 8, true, true))
            {
                handler(client, flags, ms);
            }
        }

        private void HandleHandshakeMessage(FramedClient client, ActorProtocolFlags flags, MemoryStream ms)
        {
            var req = serializer.Deserialize<HandshakeRequest>(flags, "Handshake", ms);

            AuxSendProtocolPacket(client, "Handshake", ActorProtocol.HandshakeId,
                new HandshakeResponse
                {
                    RequestedProtocolVersion = req.ClientProtocolVersion,
                    ServerProtocolVersion = ActorProtocol.Version,
                    ProtocolMatch = req.ClientProtocolVersion == ActorProtocol.Version
                });
        }

        private void HandlePingMessage(FramedClient client, ActorProtocolFlags flags, MemoryStream ms)
        {
            clientTimestamps[client] = TimeSpan.FromMilliseconds(Environment.TickCount);

            var req = serializer.Deserialize<Ping>(flags, "Ping", ms);
            AuxSendProtocolPacket(client, "Handshake", ActorProtocol.PingId,
                new Ping
                {
                    Timestamp = req.Timestamp
                });
        }

        private void HandleMessage(FramedClient client, ActorProtocolFlags flags, long reqId, string messageName, MemoryStream ms)
        {
            Action<FramedClient, ActorProtocolFlags, string, long, MemoryStream> handler;

            if (!handlers.TryGetValue(messageName, out handler))
            {
                throw new Exception(
                    $"Client {client.RemoteEndPoint} sent message for method {messageName}, which has no handler registered");
            }

            if (options.ActorSessionInjectionEnabled)
            {
                try
                {
                    CallContext.LogicalSetData(ActorSession.ActorSessionCallContextKey, actorSessions[client]);
                    handler(client, flags, messageName, reqId, ms);
                }
                finally
                {
                    CallContext.FreeNamedDataSlot(ActorSession.ActorSessionCallContextKey);
                }                
            }
            else
            {
                handler(client, flags, messageName, reqId, ms);
            }

        }

        protected void HandleResponseNoResult(FramedClient client, ActorProtocolFlags flags, string messageName, long reqId, Task actorResponse,
            IReplyMessage<Unit> msgToSend)
        {
            if (actorResponse == null)
            {
                Send(client, flags, messageName, reqId, msgToSend);
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

                    Send(client, flags, messageName, reqId, msgToSend);
                });
            }
        }

        protected void HandleResponse<R>(FramedClient client, ActorProtocolFlags flags, string messageName, long reqId, Task<R> actorResponse,
            IReplyMessage<R> msgToSend)
        {
            if (actorResponse == null)
            {
                Send(client, flags, messageName, reqId, msgToSend);
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

                    Send(client, flags, messageName, reqId, msgToSend);
                });
            }
        }

        private unsafe void Send<R>(IFramedClient client, ActorProtocolFlags flags, string messageName, long requestId, IReplyMessage<R> packet)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(12);
                ms.Position = 12;
                serializer.Serialize(flags, messageName, packet, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(ActorProtocolFlags*) buf = ActorProtocolFlags.RequestReponse;
                    *(long*) (buf + 4) = requestId;
                }

                client.SendPacket(new ArraySegment<byte>(buffer, 0, (int) ms.Length));
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
                serializer.Serialize(ActorProtocolFlags.Observable, name, msg, ms);

                ms.Position = 0;
                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(ActorProtocolFlags*) buf = ActorProtocolFlags.Observable;
                    *(int*) (buf + 4) = nameBytes.Length;
                }

                var packet = new ArraySegment<byte>(buffer, 0, (int) ms.Length);
                executor.Enqueue(() =>
                {
                    foreach (var c in clients)
                    {
                        c.SendPacket(packet);
                    }
                });
            }
        }

        private unsafe void AuxSendProtocolPacket<P>(FramedClient client, string messageName, int packetId, P packet)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(8);
                ms.Position = 8;
                serializer.Serialize(ActorProtocolFlags.StacksProtocol, messageName, packet, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(ActorProtocolFlags*) buf = ActorProtocolFlags.StacksProtocol;
                    *(int*) (buf + 4) = packetId;
                }

                client.SendPacket(new ArraySegment<byte>(buffer, 0, (int) ms.Length));
            }
        }
    }
}