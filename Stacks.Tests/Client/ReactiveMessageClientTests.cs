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

        public class Receive : Base
        {
            [Fact]
            public void Receiving_packet_should_be_deserialized_properly()
            {
                bool received = false;
                serializer.Setup(s => s.CreateDeserializer<TestData>()).Returns((MemoryStream ms) => new TestData());

                var c = new ReactiveMessageClient<ITestMessageHandler>(framedClient, serializer.Object);
                c.Packets.TestPackets.Subscribe(p =>
                    {
                        received = true;
                    });

                rawClientReceived.OnNext(new ArraySegment<byte>(new byte[] { 12, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0 }));

                Assert.True(received);
            }
        }

        [StacksMessage(3)]
        public class TestData
        {
            public int Foo { get; set; }
            public double Bar { get; set; }
            public decimal Zar { get; set; }
            public string Sar { get; set; }
        }

        public interface ITestMessageHandler
        {
            IObservable<TestData> TestPackets { get; }
        }
    }
}
