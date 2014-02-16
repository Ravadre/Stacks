using Stacks;
using Stacks.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RawStreamPerfTest
{
    class Program
    {
        static SocketServer s;
        static SocketClient c1, c2;

        static void Main(string[] args)
        {
            
            ServerHelpers.CreateServerAndConnectedClient(out s, out c1, out c2);

            c1.Disconnected += exn => { Console.WriteLine("C1 d/c " + exn); };
            c2.Disconnected += exn => { Console.WriteLine("C2 d/c " + exn); };
            
            Measure(8192, 8192);
           // Console.ReadLine();
            Measure(1, 8192 * 8192 / 16);

           // Console.ReadLine();
        }

        private static void Measure(int bufSize, int packets)
        {
            var buffer = DataHelpers.CreateRandomBuffer(bufSize);
            long l = packets;
            long totalRecv = 0;
            var received = new ManualResetEventSlim();

            GC.Collect();
            Console.WriteLine("Gen 0: " + GC.CollectionCount(0) +
                ", Gen 1: " + GC.CollectionCount(1) + ", Gen 2: " + 
                GC.CollectionCount(2));
            var sw = Stopwatch.StartNew();

            Action<ArraySegment<byte>> recv = bs =>
                {
                    totalRecv += bs.Count;
                    if (totalRecv == l * bufSize) received.Set();
                };

            c2.Received += recv;

            for (int i = 0; i < l; ++i)
            {
                c1.Send(buffer);
            }

            received.Wait();

            c2.Received -= recv;

            var elapsed = sw.ElapsedMilliseconds;
            GC.Collect();
            Console.WriteLine("Gen 0: " + GC.CollectionCount(0) +
                ", Gen 1: " + GC.CollectionCount(1) + ", Gen 2: " +
                GC.CollectionCount(2));

            Console.WriteLine("Elapsed ms: " + elapsed);
        }
    }
}
