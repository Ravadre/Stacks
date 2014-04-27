using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Stacks.Serializers;

using Moq;
using Xunit;

using MsgPack;
using MsgPack.Serialization;
using System.IO;
using System.ComponentModel;
using System.Threading;

namespace Stacks.Tests.Serialization
{
    public class MessagePackSerializerTests
    {
        protected MemoryStream CreateSerializedObject<T>(T sample)
        {
            var serializer = MessagePackSerializer.Create<T>();
            var ms = new MemoryStream();

            serializer.Pack(ms, sample);

            ms.Position = 0;
            
            return ms;
        }

        protected TestData CreateSampleTestData()
        {
            return new TestData { Bar = Math.PI , Zar = (decimal)Math.PI, Foo = 42, Sar = "Foo bar test" };
        }

        public class Deserialize_should : MessagePackSerializerTests
        {
            [Fact]
            public void Call_handler_when_packet_is_received_with_data_deserialized()
            {
                var test = CreateSampleTestData();
                var obj = CreateSerializedObject(test);

                Console.WriteLine(new ArraySegment<byte>(obj.GetBuffer(), 0, (int)obj.Length).ToBinaryString());

                var m = new Mock<TestDataHandler>();
                m.Setup(s => s.HandleTestData(It.IsAny<TestData>())).Callback( (TestData data) =>
                    {
                        Assert.Equal(test.Bar, data.Bar);
                        Assert.Equal(test.Foo, data.Foo);
                        Assert.Equal(test.Sar, data.Sar);
                        Assert.Equal(test.Zar, data.Zar);
                    });
                
                var serializer = new MessagePackStacksSerializer(m.Object);
                serializer.Deserialize(3, obj);

                m.Verify(x => x.HandleTestData(It.IsAny<TestData>()), Times.Once());
            }

            [Fact]
            public void Call_appropriate_handler_when_multiple_handlers_are_present()
            {
                var test = CreateSampleTestData();
                var obj = CreateSerializedObject(test);

                var m = new Mock<MultiTestDataHandler>();
                m.Setup(s => s.HandleTestData(It.IsAny<TestData>())).Callback((TestData data) =>
                {
                    Assert.Equal(test.Bar, data.Bar);
                    Assert.Equal(test.Foo, data.Foo);
                    Assert.Equal(test.Sar, data.Sar);
                    Assert.Equal(test.Zar, data.Zar);
                });

                var serializer = new MessagePackStacksSerializer(m.Object);
                serializer.Deserialize(1, obj);

                m.Verify(x => x.HandleTestData(It.IsAny<TestData>()), Times.Once());
                m.Verify(x => x.HandleTestData2(It.IsAny<TestData2>()), Times.Never());
                m.Verify(x => x.HandleTestData3(It.IsAny<TestData2>()), Times.Never());
                m.Verify(x => x.HandleTestData4(It.IsAny<TestData3>()), Times.Never());
                m.Verify(x => x.HandleTestData5(It.IsAny<TestData3>()), Times.Never());
            }

            [Fact]
            public void Throw_exception_when_trying_to_deserialize_packet_without_handler()
            {
                var test = CreateSampleTestData();
                var obj = CreateSerializedObject(test);

                var m = new Mock<TestDataHandler>();
                m.Setup(s => s.HandleTestData(It.IsAny<TestData>())).Callback((TestData data) => { });

                var serializer = new MessagePackStacksSerializer(m.Object);

                Assert.Throws(typeof(InvalidOperationException),
                    () =>
                    {
                        serializer.Deserialize(4, obj);
                    });
            }
        }


        public class Serialize_should : MessagePackSerializerTests
        {
            [Fact]
            public void Serialize_data_so_it_can_be_later_deserialized_with_Deserialize()
            {
                var test = CreateSampleTestData();
                var m = new Mock<TestDataHandler>();
                m.Setup(s => s.HandleTestData(It.IsAny<TestData>())).Callback((TestData data) =>
                {
                    Assert.Equal(test.Bar, data.Bar);
                    Assert.Equal(test.Foo, data.Foo);
                    Assert.Equal(test.Sar, data.Sar);
                    Assert.Equal(test.Zar, data.Zar);
                });

                var serializer = new MessagePackStacksSerializer(m.Object);
                var ms = new MemoryStream();

                serializer.Serialize(test, ms);
                ms.Position = 0;
                serializer.Deserialize(3, ms);

                m.Verify(x => x.HandleTestData(It.IsAny<TestData>()), Times.Once());
            }
        }



        public class TestData
        {
            public int Foo { get; set; }
            public double Bar { get; set; }
            public decimal Zar { get; set; }
            public string Sar { get; set; }
        }

        public class TestData2
        {
            public int Foo2 { get; set; }
        }

        public class TestData3
        {
            public int Foo3 { get; set; }
        }

        public abstract class TestDataHandler : IMessageHandler
        {
            [MessageHandler(3)]
            public abstract void HandleTestData(TestData data);
        }

        public abstract class MultiTestDataHandler : IMessageHandler
        {
            [MessageHandler(1)]
            public abstract void HandleTestData(TestData data);
            [MessageHandler(2)]
            public abstract void HandleTestData2(TestData2 data);
            [MessageHandler(3)]
            public abstract void HandleTestData3(TestData2 data);
            [MessageHandler(4)]
            public abstract void HandleTestData4(TestData3 data);
            [MessageHandler(5)]
            public abstract void HandleTestData5(TestData3 data);
        }
    }
}
