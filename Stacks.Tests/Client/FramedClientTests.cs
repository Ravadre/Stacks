﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Stacks.Tcp;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace Stacks.Tests
{
    public class FramedClientTests
    {
        public class Base
        {
            protected Mock<IRawByteClient> rawClient;

            public Base()
            {
                rawClient = new Mock<IRawByteClient>();
                rawClient.Setup(r => r.Received).Returns(Observable.Empty<ArraySegment<byte>>());
            }

            protected ArraySegment<byte> CreateBuffer(params byte[] bs)
            {
                return new ArraySegment<byte>(bs);
            }

            protected ArraySegment<byte> CreateBufferInt(params int[] ints)
            {
                var bs = new byte[ints.Length * 4];
                Buffer.BlockCopy(ints, 0, bs, 0, bs.Length);

                return new ArraySegment<byte>(bs);
            }

            protected IEnumerable<ArraySegment<byte>> Split(
                ArraySegment<byte> bs, params int[] offsets)
            {
                var xs = new List<ArraySegment<byte>>();
                offsets = new[] { 0 }.Concat(offsets.Concat(new[] { bs.Count })).ToArray();

                for (int i = 0; i < offsets.Length - 1; ++i)
                {
                    var co = offsets[i];
                    var no = offsets[i + 1];
                    xs.Add(
                        new ArraySegment<byte>(
                            bs.Array,
                            bs.Offset + co,
                            no - co));
                }

                return xs;
            }

            protected int ToInt(ArraySegment<byte> bs, int byteOffset)
            {
                return BitConverter.ToInt32(bs.Array, bs.Offset + byteOffset);
            }

            protected void ReceiveBytesAndAssertPacket(Action<ArraySegment<byte>> recvAsserts,
                ArraySegment<byte> recvBytes)
            {
                bool called = false;
                var bytesRecv = new Subject<ArraySegment<byte>>();
                rawClient.Setup(s => s.Received).Returns(bytesRecv);

                var c = new FramedClient(rawClient.Object);
                c.Received.Subscribe(bs =>
                {
                    called = true;
                    recvAsserts(bs);
                });


                bytesRecv.OnNext(recvBytes);

                Assert.True(called);
            }

            protected void ReceiveBytesAndAssertPackets(Action<int, ArraySegment<byte>> recvAsserts,
                ArraySegment<byte> recvBytes)
            {
                ReceiveBytesSegmentsAndAssertPackets(recvAsserts,
                    new[] { recvBytes });
            }

            protected void ReceiveBytesSegmentsAndAssertPackets(Action<int, ArraySegment<byte>> recvAsserts,
                IEnumerable<ArraySegment<byte>> recvBytes)
            {
                int idx = 0;
                
                var bytesRecv = new Subject<ArraySegment<byte>>();
                rawClient.Setup(s => s.Received).Returns(bytesRecv);

                var c = new FramedClient(rawClient.Object);
                c.Received.Subscribe(bs =>
                {
                    recvAsserts(idx++, bs);
                });

                foreach (var recv in recvBytes)
                {
                    bytesRecv.OnNext(recv);
                }
            }
        }

        public class Receive : Base
        {
            [Fact]
            public void When_full_packet_is_received_it_should_be_processed()
            {
                ReceiveBytesAndAssertPacket(bs =>
                    {
                        Assert.Equal(4, bs.Count);
                        Assert.Equal(1234, ToInt(bs, 0));
                    }, CreateBufferInt(8, 1234));
            }

            [Fact]
            public void When_two_packets_are_received_receive_packet_should_be_called_twice()
            {
                int calls = 0;
                ReceiveBytesAndAssertPackets((idx, bs) =>
                    {
                        ++calls;
                        Assert.Equal(idx + 1, ToInt(bs, 0));
                    }, CreateBufferInt(8, 1, 8, 2));

                Assert.Equal(2, calls);
            }

            [Fact]
            public void When_packet_is_receive_in_fragments_packet_should_be_received_after_it_is_fully_received()
            {
                int calls = 0;
                ReceiveBytesSegmentsAndAssertPackets((idx, bs) =>
                {
                    ++calls;
                    Assert.Equal(idx + 1, ToInt(bs, 0));
                }, Split(CreateBufferInt(8, 1, 8, 2), 1, 3, 5, 7, 13));

                Assert.Equal(2, calls);
            }

            [Fact]
            public void When_many_packets_are_received_callbacks_should_be_called_for_every_packet()
            {
                int calls = 0;
                ReceiveBytesSegmentsAndAssertPackets((idx, bs) =>
                    {
                        ++calls;
                        Assert.Equal(idx * 2, ToInt(bs, 0));
                    }, new[] 
                    {
                        CreateBufferInt(8, 0, 12, 2, 1),
                        CreateBufferInt(16, 4, 3, 2, 8, 6)
                    });

                Assert.Equal(4, calls);
            }

            [Fact]
            public void Packets_should_be_received_correctly_when_middle_packet_is_fragmented()
            {
                int calls = 0;
                ReceiveBytesSegmentsAndAssertPackets((idx, bs) =>
                    {
                        ++calls;
                        Assert.Equal(idx * 5, ToInt(bs, 0));
                    }, new[]
                    {
                        CreateBuffer(8,  0, 0, 0,
                                     0,  0, 0, 0,
                                     16, 0, 0),
                        CreateBuffer(          0,
                                     5,  0, 0, 0, 
                                     1,  2, 3, 4, 
                                     5,  6, 7, 8,
                                     8),
                        CreateBuffer(    0, 0, 0, 
                                     10, 0, 0, 0)
                    });

                Assert.Equal(3, calls);
            }
        }

        public class Send : Base
        {
            [Fact]
            public void Sending_packet_with_byte_array_should_call_raw_client_with_header_appended()
            {
                var c = new FramedClient(rawClient.Object);

                c.SendPacket(new byte[] { 1, 2, 3 });

                rawClient.Verify(r => r.Send(new byte[] { 7, 0, 0, 0, 1, 2, 3 }));
            }

            [Fact]
            public void Sending_packet_with_array_segment_should_call_raw_client_with_header_appended()
            {
                var c = new FramedClient(rawClient.Object);

                c.SendPacket(new ArraySegment<byte>(new byte[] { 0, 0, 5, 0, 0, 0, 54, 1, 2, 3 }, 6, 1));

                rawClient.Verify(r => r.Send(new byte[] { 5, 0, 0, 0, 54 }));
            }

            [Fact]
            public void Sending_preformatted_packet_should_not_add_additional_header()
            {
                var c = new FramedClient(rawClient.Object);
                var data = new byte[] { 1, 2, 3, 4, 5, 6, 7 };

                var buffer = c.PreparePacketBuffer(7);
                Buffer.BlockCopy(data, 0, buffer.Packet.Array, buffer.Packet.Offset, data.Length);

                c.SendPacket(buffer);

                rawClient.Verify(r => r.Send(new byte[] { 11, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7 }));
                Assert.Equal(11, buffer.Packet.Array.Length);
                Assert.Equal(7, buffer.Packet.Count);
            }
        }
    }
}
