using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Stacks.Actors;
using Stacks;
using Xunit;
using Stacks.Tcp;
using System.Threading;
using System.IO;
using ProtoBuf;
using System.Net.Sockets;

namespace Stacks.Tests.Remote
{
    public class TimeoutTests
    {
        private IActorServerProxy server;
        private FramedClient client;
        private IStacksSerializer serializer;

        public TimeoutTests()
        {
            serializer = new ProtoBufStacksSerializer();
        }

        [Fact(Skip = "Test takes too long to run without ability to set pinging intervals")]
        public async void Client_should_be_disconnected_after_over_30_seconds_after_last_ping()
        {
            var disconnected = new ManualResetEventSlim();
            server = ActorServerProxy.Create<ITestActor, TestActor>("tcp://*:0");
            int port = server.BindEndPoint.Port;

            client = new FramedClient(new SocketClient());
            await client.Connect("tcp://localhost:" + port);

            client.Received.Subscribe(x => Console.WriteLine("Received " + x.Count + " bytes. " +
                "Header: " + BitConverter.ToInt32(x.Array, x.Offset) + " " +
                "Id: " + BitConverter.ToInt32(x.Array, x.Offset + 4)));
            client.Disconnected.Subscribe(_ => disconnected.Set());

            SendHandshake();
            SendPing();

            Thread.Sleep(40000);
            Assert.False(disconnected.IsSet);
            disconnected.Wait(40000);
            Assert.True(disconnected.IsSet);
        }

        [Fact(Skip = "Test takes too long to run")]
        public void Client_should_receive_disconnected_event_with_timeout_after_period_of_time()
        {
            var client = new SocketClient();
            var connected = client.Connect("tcp://10.255.255.1:80");


            try
            {
                connected.Wait();
            }
            catch (SocketException exn)
            {
                Assert.Equal(SocketError.TimedOut, exn.SocketErrorCode);
            }
        }

        [Fact(Skip = "Test takes too long to run")]
        public void Actor_should_receive_disconnected_event_with_timeout_after_period_of_time()
        {
            var client = ActorClientProxy.CreateProxy<IMessageActor>("tcp://10.255.255.1:80");

            try
            {
                var proxy = client.Result;
            }
            catch (AggregateException exn)
            {
                Assert.IsType<SocketException>(exn.InnerException);
                Assert.Equal(SocketError.TimedOut, ((SocketException)exn.InnerException).SocketErrorCode);
            }
        }

        private void SendPing()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((int)4);
                writer.Write((int)2);
                writer.Flush();
                serializer.Serialize<Ping>(new Ping { Timestamp = 0 }, ms);
                client.SendPacket(new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length));
            }
        }

        private void SendHandshake()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((int)4);
                writer.Write((int)1);
                writer.Flush();
                serializer.Serialize<HandshakeRequest>(new HandshakeRequest { ClientProtocolVersion = 1 }, ms);
                client.SendPacket(new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length));
            }
        }


        [ProtoContract]
        class HandshakeRequest
        {
            [ProtoMember(1)]
            public int ClientProtocolVersion { get; set; }
        }

        [ProtoContract]
        class HandshakeResponse
        {
            [ProtoMember(1)]
            public int RequestedProtocolVersion { get; set; }
            [ProtoMember(2)]
            public int ServerProtocolVersion { get; set; }
            [ProtoMember(3)]
            public bool ProtocolMatch { get; set; }
        }

        [ProtoContract]
        class Ping
        {
            [ProtoMember(1)]
            public long Timestamp { get; set; }
        }
    }
}
