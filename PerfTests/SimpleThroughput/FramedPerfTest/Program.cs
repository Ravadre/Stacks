using Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Tcp;
using Stacks.Tests;

namespace RawStreamPerfTest
{
    class Program
    {
        static SocketServer s;
        static SocketClient c1, c2;
        static IFramedClient fc1, fc2;

        static void Main(string[] args)
        {
            ServerHelpers.CreateServerAndConnectedClient(out s, out c1, out c2);

            c1.Disconnected += exn => { Console.WriteLine("C1 d/c " + exn); };
            c2.Disconnected += exn => { Console.WriteLine("C2 d/c " + exn); };

            fc1 = new FramedClient(c1);
            fc2 = new FramedClient(c2);

            Measure(8192, 8192);
            Console.ReadLine();
            Measure(8192, 8192 * 16);

            Console.ReadLine();
        }

        private static void Measure(int bufSize, int packets)
        {
            var buffer = DataHelpers.CreateRandomBuffer(bufSize);
            long l = packets;
            long totalRecv = 0;
            long totalPacketsRecv = 0;
            var received = new ManualResetEventSlim();

            GC.Collect();
            Console.WriteLine("Gen 0: " + GC.CollectionCount(0) +
                ", Gen 1: " + GC.CollectionCount(1) + ", Gen 2: " +
                GC.CollectionCount(2));
            var sw = Stopwatch.StartNew();

            Action<ArraySegment<byte>> recv = bs =>
            {
                totalRecv += bs.Count;
                ++totalPacketsRecv;
                if (totalRecv == l * bufSize) received.Set();
            };
            Action<int> sent = (t) => Console.WriteLine("Sent ");

            fc1.Sent += sent;
            fc2.Received += recv;

            for (int i = 0; i < l; ++i)
            {
                fc1.SendPacket(buffer);
            }

            received.Wait();

            fc2.Received -= recv;

            var elapsed = sw.Elapsed.TotalSeconds;
            GC.Collect();
            Console.WriteLine("Gen 0: " + GC.CollectionCount(0) +
                ", Gen 1: " + GC.CollectionCount(1) + ", Gen 2: " +
                GC.CollectionCount(2));

            Console.WriteLine("Elapsed s: " + elapsed);
            Console.WriteLine("Rate: " + (double)totalRecv * 8 / elapsed / 1024 / 1024 + " Mb/sec");
            Console.WriteLine("Sent {0} packets. Received: {1}", packets, totalPacketsRecv);
        }
    }
}
