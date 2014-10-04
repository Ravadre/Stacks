using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks.Tcp;

namespace Stacks.Actors
{
    class ActorRemoteMessageClient
    {
        protected readonly IFramedClient framedClient;
        protected readonly IStacksSerializer packetSerializer;

        private AsyncSubject<Unit> handshakeCompleted;
        private AsyncSubject<Exception> disconnectedSubject;

        public IExecutor Executor
        {
            get { return framedClient.Executor; }
        }

        public bool IsConnected
        {
            get { return framedClient.IsConnected; }
        }

        public IObservable<Unit> Connected
        {
            get { return this.handshakeCompleted.AsObservable(); }
        }

        public IObservable<Exception> Disconnected
        {
            get { return this.disconnectedSubject.AsObservable(); }
        }

        public IObservable<int> Sent
        {
            get { return this.framedClient.Sent; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return framedClient.LocalEndPoint; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return framedClient.RemoteEndPoint; }
        }

        public IObservable<Unit> Connect(IPEndPoint endPoint)
        {
            framedClient.Connect(endPoint);

            return handshakeCompleted.AsObservable();
        }

        public event Action<long, MemoryStream> MessageReceived;
        public event Action<string, MemoryStream> ObsMessageReceived;

        public ActorRemoteMessageClient(IFramedClient client)
        {
            this.framedClient = client;
            this.packetSerializer = new ProtoBufStacksSerializer();

            this.framedClient.Received.Subscribe(PacketReceived);
            this.framedClient.Connected.Subscribe(OnConnected);
            this.framedClient.Disconnected.Subscribe(OnDisconnected);

            this.handshakeCompleted = new AsyncSubject<Unit>();
            this.disconnectedSubject = new AsyncSubject<Exception>();
        }


        private void OnConnected(Unit _)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(8);
                ms.Position = 8;
                
                packetSerializer.Serialize(
                    new Proto.HandshakeRequest()
                    {
                        ClientProtocolVersion = Proto.ActorProtocol.Version
                    }, ms);

                ms.Position = 0;

                var buffer = ms.GetBuffer();
                unsafe
                {
                    fixed (byte* b = buffer)
                    {
                        *(Proto.ActorProtocolFlags*)b = Proto.ActorProtocolFlags.StacksProtocol;
                        *(int*)(b + 4) = 1;
                    }
                }

                framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        private void OnDisconnected(Exception exn)
        {
            handshakeCompleted.OnError(exn);
            disconnectedSubject.OnNext(exn);
            disconnectedSubject.OnCompleted();
        }

        private void FailWithExnAndClose(Exception exn)
        {
            handshakeCompleted.OnError(exn);
            disconnectedSubject.OnNext(exn);
            disconnectedSubject.OnCompleted();
            
            framedClient.Close();
        }

        private void CompleteHandshake()
        {
            handshakeCompleted.OnNext(Unit.Default);
            handshakeCompleted.OnCompleted();
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int header = *(int*)b;

                if (Bit.IsSet(header, (int)Proto.ActorProtocolFlags.RequestReponse))
                {
                    long requestId = *(long*)(b + 4);

                    using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 12, buffer.Count - 12))
                    {
                        OnMessageReceived(requestId, ms);
                    }
                }
                else if (Bit.IsSet(header, (int)Proto.ActorProtocolFlags.Observable))
                {
                    var nameLen = *(int*)(b + 4);
                    var name = new string((sbyte*)b, 8, nameLen);

                    using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 8 + nameLen, buffer.Count - 8 - nameLen))
                    {
                        OnObsMessageReceived(name, ms);
                    }
                }
                else if (Bit.IsSet(header, (int)Proto.ActorProtocolFlags.StacksProtocol))
                {
                    int pid = *(int*)(b + 4);

                    if (pid != 2)
                    {
                        FailWithExnAndClose(new InvalidProtocolException("Server has incompatible protocol"));
                    }

                    using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 8 , buffer.Count - 8))
                    {
                        var resp = packetSerializer.Deserialize<Proto.HandshakeResponse>(ms);

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
                }
                else
                {
                    FailWithExnAndClose(new InvalidProtocolException("Server has incompatible protocol"));
                }
            }
        }

        private void OnMessageReceived(long requestId, MemoryStream ms)
        {
            var h = MessageReceived;
            if (h != null)
                h(requestId, ms);
        }

        private void OnObsMessageReceived(string name, MemoryStream ms)
        {
            var h = ObsMessageReceived;
            if (h != null)
                h(name, ms);
        }

        public unsafe void Send<T>(string msgName, long requestId, T obj)
        {
            var msgNameBytes = Encoding.ASCII.GetBytes(msgName);

            using (var ms = new MemoryStream())
            {
                ms.SetLength(16);
                ms.Position = 16;
                ms.Write(msgNameBytes, 0, msgNameBytes.Length);
                this.packetSerializer.Serialize(obj, ms);
                ms.Position = 0;

                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    *(Proto.ActorProtocolFlags*)buf = Proto.ActorProtocolFlags.RequestReponse;
                    *(long*)(buf + 4) = requestId;
                    *(int*)(buf + 12) = msgNameBytes.Length;
                }

                this.framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        public void Close()
        {
            this.framedClient.Close();
        }
    }
}
