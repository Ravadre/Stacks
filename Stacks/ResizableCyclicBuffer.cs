using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    internal class ResizableCyclicBuffer
    {
        private byte[] buffer;
        private int endOffset;
        private int beginOffset;

        private static IEnumerable<ArraySegment<byte>> EmptyPacketList
            = new List<ArraySegment<byte>>();

        public ResizableCyclicBuffer(int initSize)
        {
            buffer = new byte[initSize];
        }

        public int Count
        {
            get
            {
                return endOffset - beginOffset;
            }
        }

        public void AddData(ArraySegment<byte> data)
        {
            CleanupBuffer();

            while (buffer.Length - endOffset < data.Count)
                ResizeBuffer();

            Buffer.BlockCopy(data.Array, data.Offset,
                             buffer, endOffset, data.Count);
            endOffset += data.Count;
        }

        private void ResizeBuffer()
        {
            byte[] newBuffer = new byte[buffer.Length * 2];
            Buffer.BlockCopy(buffer, 0, newBuffer, 0, endOffset);
            this.buffer = newBuffer;
        }

        public int ReadRawBytes(ArraySegment<byte> buffer)
        {
            CleanupBuffer();

            int totalBytes = endOffset - beginOffset;
            int toRead = Math.Min(buffer.Count, totalBytes);

            if (toRead == 0)
                return 0;

            Buffer.BlockCopy(this.buffer, this.beginOffset,
                buffer.Array, buffer.Offset, toRead);

            this.beginOffset += toRead;

            return toRead;
        }

        public unsafe IEnumerable<ArraySegment<byte>> GetPackets()
        {
            CleanupBuffer();

            if (endOffset < 4)
                return EmptyPacketList;

            var packets = new List<ArraySegment<byte>>();

            fixed (byte* b = buffer)
            {
                byte* bPtr = b;

                while (true)
                {
                    if (beginOffset + 4 > endOffset)
                        break;

                    int size = *((int*)bPtr);

                    if (beginOffset + size > endOffset)
                        break;

                    packets.Add(new ArraySegment<byte>
                        (buffer, beginOffset, size));

                    beginOffset += size;
                    bPtr += size;
                }
            }

            return packets;
        }

        private void CleanupBuffer()
        {
            if (beginOffset == 0)
                return;

            Buffer.BlockCopy(buffer, beginOffset, buffer, 0, endOffset - beginOffset);
            endOffset -= beginOffset;
            beginOffset = 0;
        }
    }
}
