﻿using Moq;
using MsgPack.Serialization;
using Stacks.Client;
using Stacks.Serializers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Stacks.Tests
{
    public class MessageClientTests
    {
        public class Base
        {
            protected Mock<IRawByteClient> rawClient;
            protected FramedClient framedClient;
            protected Mock<IStacksSerializer> serializer; 

            public Base()
            {
                rawClient = new Mock<IRawByteClient>();
                framedClient = new FramedClient(rawClient.Object);
                serializer = new Mock<IStacksSerializer>();
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
                var c = new MessageClient(framedClient, serializer.Object);

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