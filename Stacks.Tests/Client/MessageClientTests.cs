using Moq;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Stacks.Tcp;

namespace Stacks.Tests
{
    public class MessageClientTests
    {
        public class Base
        {
            protected Mock<IRawByteClient> rawClient;
            protected FramedClient framedClient;
            protected Mock<IStacksSerializer> serializer;
            protected Mock<TestDataHandler> messageHandler;

            public Base()
            {
                rawClient = new Mock<IRawByteClient>();
                framedClient = new FramedClient(rawClient.Object);
                serializer = new Mock<IStacksSerializer>();
                messageHandler = new Mock<TestDataHandler>();
            }

            protected TestData CreateSampleTestData()
            {
                return new TestData { Bar = Math.PI, Zar = (decimal)Math.PI, Foo = 42, Sar = "Foo bar test" };
            }
        }

        public class Send : Base
        {
            [Fact]
            public void Sending_packet_should_send_serialized_data_with_proper_header()
            {
                var c = new MessageClient(framedClient, serializer.Object, messageHandler.Object);

                serializer.Setup(s => s.Serialize(It.IsAny<TestData>(), It.IsAny<MemoryStream>()))
                          .Callback((TestData d, MemoryStream ms) =>
                          {
                              ms.Write(new byte[] { 0, 1, 2, 3, 4 }, 0, 5);
                          });

                rawClient.Setup(rc => rc.Send(It.IsAny<byte[]>())).Callback((byte[] b) =>
                {
                    var length = BitConverter.ToInt32(b, 0);
                    var typeCode = BitConverter.ToInt32(b, 4);

                    Assert.Equal(4 + 4 + 5, length);
                    Assert.Equal(4 + 4 + 5, b.Length);
                    Assert.Equal(3, typeCode);
                    Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, new ArraySegment<byte>(b, 8, 5));
                });

                c.Send(3, CreateSampleTestData());
            }
        }

        public class Receive : Base
        {
            [Fact]
            public void Receiving_packet_should_be_deserialized_properly()
            {
                serializer.Setup(s => s.CreateDeserializer<TestData>()).Returns((MemoryStream ms) => new TestData());

                var c = new MessageClient(framedClient, serializer.Object, messageHandler.Object);

                rawClient.Raise(r => r.Received += delegate { }, new ArraySegment<byte>(new byte[] { 12, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0 }));

                messageHandler.Verify(m => m.HandleTestData(It.IsAny<TestData>()), Times.Once());

            }
        }



        
        public class TestData
        {
            public int Foo { get; set; }
            public double Bar { get; set; }
            public decimal Zar { get; set; }
            public string Sar { get; set; }
        }

        public abstract class TestDataHandler : IMessageHandler
        {
            [MessageHandler(3)]
            public abstract void HandleTestData(TestData data);
        }
    }
}
