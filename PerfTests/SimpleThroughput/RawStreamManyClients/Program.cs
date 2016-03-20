using Stacks;
using Stacks.Tests;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace RawStreamPerfTest
{
    class Program
    {
        static SocketServer s;
        private static SocketClient[] clients;
        private static SocketClient[] sClients; 

        static void Main(string[] args)
        {
            var ex = new ActionBlockExecutor();
            clients = new SocketClient[1000];
            sClients = new SocketClient[1000];
            int sIdx = 0;
            var server = ServerHelpers.CreateServer();
            server.Start();
            server.Started.Wait();

            server.Connected.Subscribe(c =>
            {
                sClients[Interlocked.Increment(ref sIdx) - 1] = c;          
            });

            var allConnected = new ManualResetEventSlim();
            var connectedCount = 0;
            for (var i = 0; i < clients.Length; ++i)
            {
                clients[i] = new SocketClient(ex);
                clients[i].Connected.Subscribe(_ =>
                {
                    if (Interlocked.Increment(ref connectedCount) == 1000)
                        allConnected.Set();
                });
                clients[i].Disconnected.Subscribe(exn =>
                {
                    Console.WriteLine("Could not connect: " + exn);
                });
                clients[i].Connect("tcp://localhost:" + server.BindEndPoint.Port);
            }

            allConnected.Wait();

            Measure(8192, 10);
            Console.ReadLine();
            Measure(8192, 10 * 16);

            Console.ReadLine();
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
 
            Action<ArraySegment<byte>> recv = bs =>
            {
                Interlocked.Add(ref totalRecv, bs.Count);
                //totalRecv += bs.Count;
                if (totalRecv == sClients.Length * l * bufSize) received.Set();
            };

            for (var i = 0; i < sClients.Length; ++i)
            {
                sClients[i].Received.Subscribe(recv);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < clients.Length; ++i)
            {
                for (int p = 0; p < packets; ++p)
                {
                    clients[i].Send(buffer);    
                }
            }

            received.Wait();

            var elapsed = sw.Elapsed.TotalSeconds;
            GC.Collect();
            Console.WriteLine("Gen 0: " + GC.CollectionCount(0) +
                ", Gen 1: " + GC.CollectionCount(1) + ", Gen 2: " +
                GC.CollectionCount(2));

            Console.WriteLine("Elapsed s: " + elapsed);
            Console.WriteLine("Rate: " + (double)totalRecv * 8 / elapsed / 1024 / 1024 + " Mb/sec");
        }
    }
}
