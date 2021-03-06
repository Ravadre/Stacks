﻿using System;

namespace Stacks
{
    public struct FramedClientBuffer
    {
        public ArraySegment<byte> Packet { get; private set; }
        internal byte[] InternalBuffer => Packet.Array;

        public FramedClientBuffer(int packetLength)
            : this()
        {
            var intBuffer = new byte[packetLength + 4];
            PrepareHeader(intBuffer, packetLength + 4);

            Packet = new ArraySegment<byte>(intBuffer, 4, packetLength);
        }

        public static FramedClientBuffer FromPacket(ArraySegment<byte> buffer)
        {
            Ensure.IsNotNull(buffer.Array, "buffer.Array");

            var intBuffer = new byte[buffer.Count + 4];
            PrepareHeader(intBuffer, buffer.Count + 4);

            Buffer.BlockCopy(buffer.Array, buffer.Offset, intBuffer, 4, buffer.Count);

            var framedBuffer = new FramedClientBuffer();
            framedBuffer.Packet = new ArraySegment<byte>(intBuffer, 4, buffer.Count);

            return framedBuffer;
        }

        private static unsafe void PrepareHeader(byte[] b, int p)
        {
            fixed (byte* bPtr = b)
            {
                int* iPtr = (int*)bPtr;
                *iPtr = p;
            }
        }
    }
}
