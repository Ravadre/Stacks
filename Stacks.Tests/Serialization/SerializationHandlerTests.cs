using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Stacks.Tcp;
using Xunit;

namespace Stacks.Tests.Serialization
{
    public class SerializationHandlerTests
    {
        protected Mock<IStacksSerializer> serializer;
        protected Mock<IMessageHandler> messageHandler;
        protected Mock<IMessageClient> messageClient;

        public SerializationHandlerTests()
        {
            serializer = new Mock<IStacksSerializer>();
            messageClient = new Mock<IMessageClient>();
            messageHandler = new Mock<IMessageHandler>();
        }

        protected TestData CreateSampleTestData()
        {
            return new TestData { Bar = Math.PI, Zar = (decimal)Math.PI, Foo = 42, Sar = "Foo bar test" };
        }

        protected TestData2 CreateSampleTestData2()
        {
            return new TestData2 { Foo2 = 42 };
        }

        [Fact]
        public void Serialize_should_call_implemented_serializer()
        {
            var ser = new StacksSerializationHandler(new MessageTypeCodeCache(), 
                            messageClient.Object, serializer.Object, messageHandler.Object);

            var test = CreateSampleTestData();
            var ms = new MemoryStream();

            ser.Serialize<TestData>(test, ms);

            serializer.Verify(s => s.Serialize<TestData>(test, ms));
        }

        [Fact]
        public void Deserialize_should_call_appropriate_handler()
        {
            var h = new Mock<MultiTestDataHandler>();
            var data = CreateSampleTestData2();
            serializer.Setup(s => s.CreateDeserializer<TestData>()).Returns(ms => CreateSampleTestData());
            serializer.Setup(s => s.CreateDeserializer<TestData2>()).Returns(ms => CreateSampleTestData2());
            serializer.Setup(s => s.CreateDeserializer<TestData3>()).Returns(ms => new TestData3());

            h.Setup(m => m.HandleTestData2(It.IsAny<IMessageClient>(), It.IsAny<TestData2>())).Callback((IMessageClient _, TestData2 c) =>
                {
                    Assert.Equal(data.Foo2, c.Foo2);
                });

            var ser = new StacksSerializationHandler(new MessageTypeCodeCache(), 
                            messageClient.Object, serializer.Object, h.Object);
            ser.Deserialize(2, new MemoryStream());

            h.Verify(m => m.HandleTestData3(It.IsAny<IMessageClient>(), It.IsAny<TestData2>()), Times.Once());
        }
        
        [Fact]
        public void Deserialize_should_throw_exception_when_trying_to_deserialize_packet_without_handler()
        {
            var h = new Mock<TestDataHandler>();
            serializer.Setup(s => s.CreateDeserializer<TestData>()).Returns(ms => CreateSampleTestData());

            var ser = new StacksSerializationHandler(new MessageTypeCodeCache(), 
                            messageClient.Object, serializer.Object, h.Object);

            Assert.Throws(typeof(InvalidOperationException),
                () =>
                {
                    ser.Deserialize(4, new MemoryStream());
                });
        }

        [StacksMessage(3)]
        public class TestData
        {
            public int Foo { get; set; }
            public double Bar { get; set; }
            public decimal Zar { get; set; }
            public string Sar { get; set; }
        }

        [StacksMessage(2)]
        public class TestData2
        {
            public int Foo2 { get; set; }
        }

        [StacksMessage(4)]
        public class TestData3
        {
            public int Foo3 { get; set; }
        }

        public abstract class TestDataHandler : IMessageHandler
        {
            public abstract void HandleTestData(IMessageClient client, TestData data);
        }

        public abstract class MultiTestDataHandler : IMessageHandler
        {
            public abstract void HandleTestData(IMessageClient client, TestData data);
            public abstract void HandleTestData2(IMessageClient client, TestData2 data);
            public abstract void HandleTestData3(IMessageClient client, TestData2 data);
            public abstract void HandleTestData4(IMessageClient client, TestData3 data);
            public abstract void HandleTestData5(IMessageClient client, TestData3 data);
        }
    }
}
