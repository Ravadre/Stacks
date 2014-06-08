using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using ProtoBuf;
using Xunit;


//TODO: Refactor those and messagepack tests to share some code base
namespace Stacks.Tests.Serialization
{
    public class ProtoBufSerializerTests
    { 
        protected MemoryStream CreateSerializedObject<T>(T sample)
        {
            var ms = new MemoryStream();
            Serializer.Serialize<T>(ms, sample);
            ms.Position = 0;
            
            return ms;
        }

        protected TestData CreateSampleTestData()
        {
            return new TestData { Bar = Math.PI , Zar = (decimal)Math.PI, Foo = 42, Sar = "Foo bar test" };
        }

        public class Deserialize_should : ProtoBufSerializerTests
        {
            [Fact]
            public void Deserialize_object_properly()
            {
                var test = CreateSampleTestData();
                var obj = CreateSerializedObject(test);

                Console.WriteLine(new ArraySegment<byte>(obj.GetBuffer(), 0, (int)obj.Length).ToBinaryString());

                var serializer = new ProtoBufStacksSerializer();
                var data = serializer.Deserialize<TestData>(obj);

                Assert.Equal(test.Bar, data.Bar);
                Assert.Equal(test.Foo, data.Foo);
                Assert.Equal(test.Sar, data.Sar);
                Assert.Equal(test.Zar, data.Zar);
            }
        }


        public class Serialize_should : ProtoBufSerializerTests
        {
            [Fact]
            public void Serialize_data_so_it_can_be_later_deserialized_with_Deserialize()
            {
                var test = CreateSampleTestData();
                
                var serializer = new ProtoBufStacksSerializer();
                var ms = new MemoryStream();

                serializer.Serialize(test, ms);
                ms.Position = 0;
                var data = serializer.Deserialize<TestData>(ms);

                Assert.Equal(test.Bar, data.Bar);
                Assert.Equal(test.Foo, data.Foo);
                Assert.Equal(test.Sar, data.Sar);
                Assert.Equal(test.Zar, data.Zar);
            }
        }


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
    }
}
