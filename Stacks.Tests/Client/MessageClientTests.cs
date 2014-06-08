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
using System.Reactive.Subjects;

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

            protected Subject<ArraySegment<byte>> rawClientReceived;

            public Base()
            {
                rawClientReceived = new Subject<ArraySegment<byte>>();
                rawClient = new Mock<IRawByteClient>();
                rawClient.Setup(r => r.Received).Returns(rawClientReceived);

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
                    var messageId = BitConverter.ToInt32(b, 4);

                    Assert.Equal(4 + 4 + 5, length);
                    Assert.Equal(4 + 4 + 5, b.Length);
                    Assert.Equal(3, messageId);
                    Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, new ArraySegment<byte>(b, 8, 5));
                });

                c.Send(CreateSampleTestData());
            }

            [Fact]
            public void Sending_message_should_succeed_when_packet_types_are_preloaded()
            {
                var c = new MessageClient(framedClient, serializer.Object, messageHandler.Object);

                c.PreLoadTypesFromAssemblyOfType<TestData>();

                serializer.Setup(s => s.Serialize(It.IsAny<TestData>(), It.IsAny<MemoryStream>()))
                          .Callback((TestData d, MemoryStream ms) =>
                          {
                              ms.Write(new byte[] { 0, 1, 2, 3, 4 }, 0, 5);
                          });

                rawClient.Setup(rc => rc.Send(It.IsAny<byte[]>())).Callback((byte[] b) =>
                {
                    var length = BitConverter.ToInt32(b, 0);
                    var messageId = BitConverter.ToInt32(b, 4);

                    Assert.Equal(4 + 4 + 5, length);
                    Assert.Equal(4 + 4 + 5, b.Length);
                    Assert.Equal(3, messageId);
                    Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, new ArraySegment<byte>(b, 8, 5));
                });

                c.Send(CreateSampleTestData());
            }

            [Fact]
            public void Sending_message_should_throw_data_exception_if_sent_message_has_no_MessageId()
            {
                var c = new MessageClient(framedClient, serializer.Object, messageHandler.Object);

                Assert.Throws(typeof(InvalidDataException), () =>
                    {
                        c.Send(new TestDataWithoutMessageId());
                    });
            }


            [Fact]
            public void Sending_message_should_succeed_if_message_was_declared_imperatively()
            {
                var c = new MessageClient(framedClient, serializer.Object, new Mock<TestDataWithoutMessageIdHandler>().Object,
                    r => r.RegisterMessage<TestDataWithoutMessageId>(3));
           
                serializer.Setup(s => s.Serialize(It.IsAny<TestDataWithoutMessageId>(), It.IsAny<MemoryStream>()))
                         .Callback((TestDataWithoutMessageId d, MemoryStream ms) =>
                         {
                             ms.Write(new byte[] { 0, 1, 2, 3, 4 }, 0, 5);
                         });

                rawClient.Setup(rc => rc.Send(It.IsAny<byte[]>())).Callback((byte[] b) =>
                {
                    var length = BitConverter.ToInt32(b, 0);
                    var messageId = BitConverter.ToInt32(b, 4);

                    Assert.Equal(4 + 4 + 5, length);
                    Assert.Equal(4 + 4 + 5, b.Length);
                    Assert.Equal(3, messageId);
                    Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, new ArraySegment<byte>(b, 8, 5));
                });

                c.Send(new TestDataWithoutMessageId());
            }

            [Fact]
            public void Sending_message_should_fail_if_message_handler_has_more_messages_than_registered_imperatively()
            {
                Assert.Throws(typeof(InvalidOperationException), () =>
                    {
                        var c = new MessageClient(framedClient, serializer.Object,
                                        new Mock<BrokenTestDataWithoutMessageIdHandler>().Object,
                                        r => r.RegisterMessage<TestDataWithoutMessageId>(2));
                    });
            }
        }

        public class Receive : Base
        {
            [Fact]
            public void Receiving_packet_should_be_deserialized_properly()
            {
                serializer.Setup(s => s.Deserialize<TestData>(It.IsAny<MemoryStream>())).Returns(new TestData());

                var c = new MessageClient(framedClient, serializer.Object, messageHandler.Object);

                rawClientReceived.OnNext(new ArraySegment<byte>(new byte[] { 12, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0 }));

                messageHandler.Verify(m => m.HandleTestData(It.IsAny<IMessageClient>(), It.IsAny<TestData>()), Times.Once());

            }
        }

        public class TestDataWithoutMessageId
        {
            public int Foo { get; set; }
        }

        [StacksMessage(3)]
        public class TestData
        {
            public int Foo { get; set; }
            public double Bar { get; set; }
            public decimal Zar { get; set; }
            public string Sar { get; set; }
        }

        public abstract class TestDataHandler : IMessageHandler
        {
            public abstract void HandleTestData(IMessageClient client, TestData data);
        }

        public abstract class TestDataWithoutMessageIdHandler : IMessageHandler
        {
            public abstract void HandleTestData(IMessageClient client, TestDataWithoutMessageId data);
        }

        public abstract class BrokenTestDataWithoutMessageIdHandler : IMessageHandler
        {
            public abstract void HandleTestData(IMessageClient client, TestDataWithoutMessageId data);
            public abstract void HandleTestData(IMessageClient client, TestData data);
        }
    }
}
