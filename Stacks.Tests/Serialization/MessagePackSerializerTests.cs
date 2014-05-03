using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            public void Deserialize_object_properly()
            {
                var test = CreateSampleTestData();
                var obj = CreateSerializedObject(test);

                Console.WriteLine(new ArraySegment<byte>(obj.GetBuffer(), 0, (int)obj.Length).ToBinaryString());

                var serializer = new MessagePackStacksSerializer();
                serializer.Initialize();
                var recv = serializer.CreateDeserializer<TestData>()(obj);

                Assert.Equal(test.Foo, recv.Foo);
                Assert.Equal(test.Bar, recv.Bar);
                Assert.Equal(test.Sar, recv.Sar);
                Assert.Equal(test.Zar, recv.Zar);
            }
        }

        public class Serialize_should : MessagePackSerializerTests
        {
            [Fact]
            public void Serialize_data_so_it_can_be_later_deserialized_with_Deserialize()
            {
                var test = CreateSampleTestData();

                var serializer = new MessagePackStacksSerializer();
                serializer.Initialize();
                var ms = new MemoryStream();

                serializer.Serialize(test, ms);
                ms.Position = 0;
                var data = serializer.CreateDeserializer<TestData>()(ms);

                Assert.Equal(test.Bar, data.Bar);
                Assert.Equal(test.Foo, data.Foo);
                Assert.Equal(test.Sar, data.Sar);
                Assert.Equal(test.Zar, data.Zar);
            }
        }



        public class TestData
        {
            public int Foo { get; set; }
            public double Bar { get; set; }
            public decimal Zar { get; set; }
            public string Sar { get; set; }
        }
    }
}
