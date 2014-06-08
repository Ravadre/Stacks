using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Moq;
using MsgPack.Serialization;
using ProtoBuf;
using Stacks.Tcp;
using Xunit;

namespace Stacks.Tests
{
    public class ReactiveMessageClientTests
    {
        public class Base
        {
            protected Mock<IRawByteClient> rawClient;
            protected FramedClient framedClient;
            protected Mock<IStacksSerializer> serializer;

            protected Subject<ArraySegment<byte>> rawClientReceived;

            public Base()
            {
                rawClientReceived = new Subject<ArraySegment<byte>>();
                rawClient = new Mock<IRawByteClient>();
                rawClient.Setup(r => r.Received).Returns(rawClientReceived);

                framedClient = new FramedClient(rawClient.Object);
                serializer = new Mock<IStacksSerializer>();
            }

            protected TestData CreateSampleTestData()
            {
                return new TestData { Bar = Math.PI, Zar = (decimal)Math.PI, Foo = 42, Sar = "Foo bar test" };
            }
        }

        public class General : Base
        {
            [Fact]
            public void Subject_should_not_be_exposed_directly()
            {
                var client = new ReactiveMessageClient<ITestMessageHandler>(framedClient, serializer.Object);

                var observable = client.Packets.TestPackets;

                Assert.False(observable.GetType().GetGenericTypeDefinition() == typeof(Subject<>));
            }
        }

        public class Receive : Base
        {
            [Fact]
            public void Receiving_packet_should_be_notified_properly()
            {
                bool received = false;
                serializer.Setup(s => s.Deserialize<TestData>(It.IsAny<MemoryStream>())).Returns(new TestData());

                var c = new ReactiveMessageClient<ITestMessageHandler>(framedClient, serializer.Object);
                c.Packets.TestPackets.Subscribe(p =>
                    {
                        received = true;
                    });

                rawClientReceived.OnNext(new ArraySegment<byte>(new byte[] { 12, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0 }));

                Assert.True(received);
            }
            
            [Fact]
            public void Receiving_packet_should_be_properly_deserialized()
            {
                var received = false;
                var serializer = new ProtoBufStacksSerializer();
                var packet = new MemoryStream();
                serializer.Serialize(CreateSampleTestData(), packet);

                var client = new ReactiveMessageClient<ITestMessageHandler>(framedClient, serializer);
                client.Packets.TestPackets.Subscribe(p =>
                    {
                        Assert.Equal(42, p.Foo);
                        Assert.Equal(Math.PI, p.Bar);
                        Assert.Equal((decimal)Math.PI, p.Zar);
                        Assert.Equal("Foo bar test", p.Sar);
                        received = true;
                    });

                
                rawClientReceived.OnNext(new ArraySegment<byte>(BitConverter.GetBytes((int)packet.Length + 8)));
                rawClientReceived.OnNext(new ArraySegment<byte>(BitConverter.GetBytes(3)));
                rawClientReceived.OnNext(new ArraySegment<byte>(packet.GetBuffer(), 0, (int)packet.Length));

                Assert.True(received);
            }

            [Fact]
            public void When_more_packets_are_registered_only_valid_observer_should_be_notified()
            {
                var validReceived = false;
                var invalidReceived = false;
                var serializer = new ProtoBufStacksSerializer();
                var packet = new MemoryStream();
                serializer.Serialize(new TestData2 { Bar = 6 }, packet);

                var client = new ReactiveMessageClient<IComplexTestMessageHandler>(framedClient, serializer);
                client.Packets.TestPackets.Subscribe(p =>
                {
                    invalidReceived = true;
                });
                client.Packets.TestPackets2.Subscribe(p =>
                {
                    validReceived = true;
                });


                rawClientReceived.OnNext(new ArraySegment<byte>(BitConverter.GetBytes((int)packet.Length + 8)));
                rawClientReceived.OnNext(new ArraySegment<byte>(BitConverter.GetBytes(1)));
                rawClientReceived.OnNext(new ArraySegment<byte>(packet.GetBuffer(), 0, (int)packet.Length));

                Assert.True(validReceived);
                Assert.False(invalidReceived);
            }
        }

        [StacksMessage(3)]
        [ProtoContract]
        public class TestData
        {
            [ProtoMember(1)]
            public int Foo { get; set; }
            [ProtoMember(2)]
            public double Bar { get; set; }
            [ProtoMember(3)]
            public decimal Zar { get; set; }
            [ProtoMember(4)]
            public string Sar { get; set; }
        }

        [StacksMessage(1)]
        [ProtoContract]
        public class TestData2
        {
            [ProtoMember(1)]
            public int Bar { get; set; }
        }

        public interface ITestMessageHandler
        {
            IObservable<TestData> TestPackets { get; }
        }

        public interface IComplexTestMessageHandler
        {
            IObservable<TestData> TestPackets { get; }
            IObservable<TestData2> TestPackets2 { get; }

        }
    }
}
