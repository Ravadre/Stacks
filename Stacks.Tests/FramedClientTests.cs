using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;
using Moq;
using Stacks.Client;

namespace Stacks.Tests
{
    public class FramedClienTests
    {
        public class Receive
        {
            Mock<IRawByteClient> rawClient;

            public Receive()
            {
                rawClient = new Mock<IRawByteClient>();
            }

            private ArraySegment<byte> CreateBuffer(params byte[] bs)
            {
                return new ArraySegment<byte>(bs);
            }

            private ArraySegment<byte> CreateBufferInt(params int[] ints)
            {
                var bs = new byte[ints.Length * 4];
                Buffer.BlockCopy(ints, 0, bs, 0, bs.Length);

                return new ArraySegment<byte>(bs);
            }

            private IEnumerable<ArraySegment<byte>> Split(
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

            private int ToInt(ArraySegment<byte> bs, int byteOffset)
            {
                return BitConverter.ToInt32(bs.Array, bs.Offset + byteOffset);
            }

            private void ReceiveBytesAndAssertPacket(Action<ArraySegment<byte>> recvAsserts,
                ArraySegment<byte> recvBytes)
            {
                bool called = false;
                var c = new FramedClient(rawClient.Object);
                c.Received += bs =>
                    {
                        called = true;
                        recvAsserts(bs);
                    };

                rawClient.Raise(r => r.Received += null,
                    recvBytes);

                Assert.True(called);
            }

            private void ReceiveBytesAndAssertPackets(Action<int, ArraySegment<byte>> recvAsserts,
                ArraySegment<byte> recvBytes)
            {
                ReceiveBytesSegmentsAndAssertPackets(recvAsserts,
                    new[] { recvBytes });
            }

            private void ReceiveBytesSegmentsAndAssertPackets(Action<int, ArraySegment<byte>> recvAsserts,
                IEnumerable<ArraySegment<byte>> recvBytes)
            {
                int idx = 0;
                var c = new FramedClient(rawClient.Object);
                c.Received += bs =>
                {
                    recvAsserts(idx++, bs);
                };

                foreach (var recv in recvBytes)
                {
                    rawClient.Raise(r => r.Received += null,
                        recv);
                }
            }

            [Fact]
            public void When_full_packet_is_received_it_should_be_processed()
            {
                ReceiveBytesAndAssertPacket(bs =>
                    {
                        Assert.Equal(8, bs.Count);
                        Assert.Equal(8, ToInt(bs, 0));
                        Assert.Equal(1234, ToInt(bs, 4));
                    }, CreateBufferInt(8, 1234));
            }

            [Fact]
            public void When_two_packets_are_received_receive_packet_should_be_called_twice()
            {
                int calls = 0;
                ReceiveBytesAndAssertPackets((idx, bs) =>
                    {
                        ++calls;
                        Assert.Equal(idx + 1, ToInt(bs, 4));
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
                    Assert.Equal(idx + 1, ToInt(bs, 4));
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
                        Assert.Equal(idx * 2, ToInt(bs, 4));
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
                        Assert.Equal(idx * 5, ToInt(bs, 4));
                    }, new[]
                    {
                        CreateBuffer(8, 0, 0, 0,
                                     0, 0, 0, 0,
                                     16, 0, 0),
                        CreateBuffer(         0,
                                     5, 0, 0, 0, 
                                     1, 2, 3, 4, 
                                     5, 6, 7, 8,
                                     8),
                        CreateBuffer(   0, 0, 0, 
                                     15, 0, 0, 0)
                    });

                Assert.Equal(3, calls);
            }
        }
    }
}
