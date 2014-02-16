using Stacks.Executors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Tests
{
    public static class ManualResetEventSlimAssertExtensions
    {
        public static void AssertWaitFor(this ManualResetEventSlim ev, int timeout)
        {
            if (!ev.Wait(timeout))
                throw new TimeoutException();
        }
    }

    public static class ServerHelpers
    {
        public static SocketServer CreateServer(IExecutor executor)
        {
            return new SocketServer(executor, new IPEndPoint(IPAddress.Any, 0));
        }

        public static SocketServer CreateServer()
        {
            return new SocketServer(new ActionBlockExecutor("", new ActionContextExecutorSettings()),
                                    new IPEndPoint(IPAddress.Any, 0));
        }

        public static IExecutor CreateExecutor()
        {
            return new ActionBlockExecutor("", new ActionContextExecutorSettings());
        }

        public static void CreateServerAndConnectedClient(out SocketServer server, 
            out SocketClient client1, out SocketClient client2)
        {
            var connected1 = new ManualResetEventSlim(false);
            var connected2 = new ManualResetEventSlim(false);

            var ex = ServerHelpers.CreateExecutor();
            var s = ServerHelpers.CreateServer();

            SocketClient lClient = null;
            SocketClient sClient = null;

            s.Connected += c =>
            {
                sClient = c;
                connected1.Set();
            };

            s.Started += () =>
            {
                var c = new SocketClient(ex);
                c.Connected += () =>
                {
                    lClient = c;
                    connected2.Set();
                };
                c.Connect(new IPEndPoint(IPAddress.Loopback, s.BindEndPoint.Port));
            };

            s.Start();

            connected1.AssertWaitFor(3000);
            connected2.AssertWaitFor(3000);

            server = s;
            client1 = lClient;
            client2 = sClient;
        }
    }

    public static class SocketServerExtensions
    {
        public static void StopAndAssertStopped(this SocketServer server)
        {
            var stopped = new ManualResetEventSlim(false);
            server.Stopped += () =>
            {
                stopped.Set();
            };
            server.Stop();
            stopped.AssertWaitFor(3000);
        }
    }

    public static class SocketClientExtensions
    {
        public static byte[] ReceiveData(this SocketClient client, int totalExpectedBytes, int timeout,
            Action sendAction)
        {
            var ev = new ManualResetEventSlim();
            var buffer = new List<byte>();

            client.Received += bs =>
                {
                    lock (buffer)
                    {
                        buffer.AddRange(bs);
                    }
                };

            sendAction();
            
            var sw = Stopwatch.StartNew();
            while(true)
            {
                lock(buffer)
                {
                    if (buffer.Count == totalExpectedBytes)
                        break;
                }

                Thread.Sleep(50);

                if (sw.ElapsedMilliseconds > timeout)
                    throw new TimeoutException();
            }

            lock(buffer)
            {
                return buffer.ToArray();
            }
        }
    }
}
