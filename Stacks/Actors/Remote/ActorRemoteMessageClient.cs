using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks.Actors.Proto;
using Stacks.Actors.Remote;
using Stacks.Tcp;

namespace Stacks.Actors
{
    class ActorRemoteMessageClient
    {
        protected readonly IFramedClient framedClient;
        protected readonly ActorPacketSerializer packetSerializer;

        private readonly AsyncSubject<Unit> handshakeCompleted;
        private readonly AsyncSubject<Exception> disconnectedSubject;

        private readonly Dictionary<int, Action<IntPtr, MemoryStream>> protocolHandlers;

        private readonly Timer pingTimer;

        public IExecutor Executor => framedClient.Executor;

        public bool IsConnected => framedClient.IsConnected;

        public IObservable<Unit> Connected => handshakeCompleted.AsObservable();
        public IObservable<Exception> Disconnected => disconnectedSubject.AsObservable();
        public IObservable<int> Sent => framedClient.Sent;

        public IPEndPoint LocalEndPoint => framedClient.LocalEndPoint;
        public IPEndPoint RemoteEndPoint => framedClient.RemoteEndPoint;

        public IObservable<Unit> Connect(IPEndPoint endPoint)
        {
            framedClient.Connect(endPoint);

            return handshakeCompleted.AsObservable();
        }

        public event Action<long, MemoryStream> MessageReceived;
        public event Action<string, MemoryStream> ObsMessageReceived;

        public ActorRemoteMessageClient(ActorPacketSerializer serializer, IFramedClient client)
        {
            framedClient = client;
            packetSerializer = serializer;

            framedClient.Received.Subscribe(PacketReceived);
            framedClient.Connected.Subscribe(OnConnected);
            framedClient.Disconnected.Subscribe(OnDisconnected);

            handshakeCompleted = new AsyncSubject<Unit>();
            disconnectedSubject = new AsyncSubject<Exception>();

            protocolHandlers = new Dictionary<int, Action<IntPtr, MemoryStream>>
            {
                [ActorProtocol.HandshakeId] = HandleHandshakeMessage,
                [ActorProtocol.PingId] = HandlePingMessage
            };

            pingTimer = new Timer(OnPingTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnConnected(Unit _)
        {
            AuxSendProtocolPacket(ActorProtocol.HandshakeId, "Handshake",
                    new HandshakeRequest
                    {
                        ClientProtocolVersion = ActorProtocol.Version
                    });
        }

        private void OnDisconnected(Exception exn)
        {
            handshakeCompleted.OnError(exn);
            disconnectedSubject.OnNext(exn);
            disconnectedSubject.OnCompleted();

            pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
            pingTimer.Dispose();
        }

        private void FailWithExnAndClose(Exception exn)
        {
            handshakeCompleted.OnError(exn);
            disconnectedSubject.OnNext(exn);
            disconnectedSubject.OnCompleted();

            framedClient.Close();

            pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
            pingTimer.Dispose();
        }

        private void CompleteHandshake()
        {
            handshakeCompleted.OnNext(Unit.Default);
            handshakeCompleted.OnCompleted();

            pingTimer.Change(0, 10000);
        }

        private unsafe void HandleReqRespMessage(byte* b, ArraySegment<byte> buffer)
        {
            long requestId = *(long*)(b + 4);

            using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 12, buffer.Count - 12, true, true))
            {
                OnMessageReceived(requestId, ms);
            }
        }

        private void OnMessageReceived(long requestId, MemoryStream ms)
        {
            MessageReceived?.Invoke(requestId, ms);
        }

        private void OnObsMessageReceived(string name, MemoryStream ms)
        {
            ObsMessageReceived?.Invoke(name, ms);
        }

        private unsafe void HandleObservableMessage(byte* b, ArraySegment<byte> buffer)
        {
            var nameLen = *(int*)(b + 4);
            var name = new string((sbyte*)b, 8, nameLen);

            using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 8 + nameLen, buffer.Count - 8 - nameLen, true, true))
            {
                OnObsMessageReceived(name, ms);
            }
        }

        private unsafe void HandleProtocolMessage(byte* b, ArraySegment<byte> buffer)
        {
            int pid = *(int*)(b + 4);

            Action<IntPtr, MemoryStream> handler;
            bool hasHandler = protocolHandlers.TryGetValue(pid, out handler);

            if (!hasHandler)
                FailWithExnAndClose(new InvalidProtocolException("Server has incompatible protocol"));

            using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 8, buffer.Count - 8, true, true))
            {
                Debug.Assert(handler != null, "handler != null");
                handler(new IntPtr(b), ms);
            }
        }

        private void HandleHandshakeMessage(IntPtr p, MemoryStream ms)
        {
            var resp = packetSerializer.Deserialize<HandshakeResponse>(ActorProtocolFlags.StacksProtocol, "Handshake", ms);

            if (resp.ProtocolMatch)
            {
                CompleteHandshake();
            }
            else
            {
                FailWithExnAndClose(new InvalidProgramException(
                    "Server has incompatible protocol. Server version: " + resp.ServerProtocolVersion +
                    ". Client version: " + resp.RequestedProtocolVersion));
            }
        }


        private void OnPingTimer(object state)
        {
            Executor.Enqueue(() =>
                {
                    try
                    {
                        if (disconnectedSubject.IsCompleted)
                        {
                            pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                            pingTimer.Dispose();
                        }
                        else
                        {
                            AuxSendProtocolPacket(ActorProtocol.PingId, "Ping",
                                new Ping
                                {
                                    Timestamp = Stopwatch.GetTimestamp()
                                });
                        }
                    }
                    catch (Exception exn)
                    {
                        FailWithExnAndClose(exn);
                    }
                });
        }

        private void HandlePingMessage(IntPtr p, MemoryStream ms)
        {

        }


        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            try
            {
                fixed (byte* b = &buffer.Array[buffer.Offset])
                {
                    int header = *(int*)b;

                    if (Bit.IsSet(header, (int)ActorProtocolFlags.RequestReponse))
                        HandleReqRespMessage(b, buffer);
                    else if (Bit.IsSet(header, (int)ActorProtocolFlags.Observable))
                        HandleObservableMessage(b, buffer);
                    else if (Bit.IsSet(header, (int)ActorProtocolFlags.StacksProtocol))
                        HandleProtocolMessage(b, buffer);
                    else
                        FailWithExnAndClose(new InvalidProtocolException("Server has incompatible protocol"));
                }
            }
            catch (Exception exn)
            {
                FailWithExnAndClose(exn);
            }
        }


        public unsafe void Send<T>(string msgName, long requestId, T obj)
        {
            var msgNameBytes = Encoding.ASCII.GetBytes(msgName);

            using (var ms = new MemoryStream())
            {
                ms.SetLength(16);
                ms.Position = 16;
                ms.Write(msgNameBytes, 0, msgNameBytes.Length);
                this.packetSerializer.Serialize(ActorProtocolFlags.RequestReponse, msgName, obj, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(ActorProtocolFlags*)buf = ActorProtocolFlags.RequestReponse;
                    *(long*)(buf + 4) = requestId;
                    *(int*)(buf + 12) = msgNameBytes.Length;
                }

                this.framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        public void Close()
        {
            framedClient.Close();
        }

        private void AuxSendProtocolPacket<T>(int packetId, string packetName, T packet)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(8);
                ms.Position = 8;

                packetSerializer.Serialize(ActorProtocolFlags.StacksProtocol, packetName, packet, ms);

                ms.Position = 0;

                var buffer = ms.GetBuffer();
                unsafe
                {
                    fixed (byte* b = buffer)
                    {
                        *(ActorProtocolFlags*)b = ActorProtocolFlags.StacksProtocol;
                        *(int*)(b + 4) = packetId;
                    }
                }

                framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }
    }
}
