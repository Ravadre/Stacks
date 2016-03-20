using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace Stacks.Tests
{
    public static class ManualResetEventSlimAssertExtensions
    {
        public static void AssertWaitFor(this ManualResetEventSlim ev, int timeout)
        {
            if (!ev.Wait(timeout))
                throw new TimeoutException();
        }

        public static void AssertWaitFor(this ManualResetEventSlim ev)
        {
            ev.AssertWaitFor(5000);
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
            return new SocketServer(new ActionBlockExecutor("", new ActionBlockExecutorSettings()),
                                    new IPEndPoint(IPAddress.Any, 0));
        }

        public static SocketServer CreateServerIPv6()
        {
            return new SocketServer(new ActionBlockExecutor("", new ActionBlockExecutorSettings()),
                                    new IPEndPoint(IPAddress.IPv6Any, 0));
        }

        public static IExecutor CreateExecutor()
        {
            return new ActionBlockExecutor("", new ActionBlockExecutorSettings());
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

            s.Connected.Subscribe(c =>
            {
                sClient = c;
                connected2.Set();
            });

            s.Started.Subscribe(_ =>
            {
                var c = new SocketClient(ex);
                c.Connected.Subscribe(u =>
                {
                    lClient = c;
                    connected1.Set();
                });
                c.Connect(new IPEndPoint(IPAddress.Loopback, s.BindEndPoint.Port));
            });

            s.Start();

            connected1.AssertWaitFor(3000);
            connected2.AssertWaitFor(3000);

            server = s;
            client1 = lClient;
            client2 = sClient;
        }
    }

    public static class SslHelpers
    {
        public static void CreateServerAndConnectedClient(out SocketServer server,
          out SslClient client1, out SslClient client2)
        {
            var connected1 = new ManualResetEventSlim(false);
            var connected2 = new ManualResetEventSlim(false);

            var ex = ServerHelpers.CreateExecutor();
            var s = ServerHelpers.CreateServer();

            SslClient lClient = null;
            SslClient sClient = null;

            var certBytesStream = Assembly.GetExecutingAssembly()
                                          .GetManifestResourceStream("Stacks.Tests.StacksTest.pfx");
            var certBytes = new BinaryReader(certBytesStream).ReadBytes((int)certBytesStream.Length);

            s.Connected.Subscribe(c =>
            {
                sClient = new SslClient(c, new X509Certificate2(certBytes));

                sClient.Connected.Subscribe(_ =>
                    {
                        connected2.Set();
                    });

                sClient.EstablishSsl();
            });

            s.Started.Subscribe(_ =>
            {
                lClient = new SslClient(new SocketClient(ex), "Stacks Test", true);

                lClient.Connected.Subscribe(__ =>
                {
                    connected1.Set();
                });

                lClient.Connect(new IPEndPoint(IPAddress.Loopback, s.BindEndPoint.Port));
            });

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
            server.Stopped.Subscribe(_ =>
            {
                stopped.Set();
            });
            server.Stop();
            stopped.AssertWaitFor(3000);
        }
    }

    public static class SocketClientExtensions
    {
        public static byte[] ReceiveData(this IRawByteClient client, int totalExpectedBytes, int timeout,
            Action sendAction)
        {
            var ev = new ManualResetEventSlim();
            var buffer = new List<byte>();

            client.Received.Subscribe(bs =>
                {
                    lock (buffer)
                    {
                        buffer.AddRange(bs);
                    }
                });

            sendAction();

            var sw = Stopwatch.StartNew();
            while (true)
            {
                lock (buffer)
                {
                    if (buffer.Count == totalExpectedBytes)
                        break;
                }

                Thread.Sleep(50);

                if (sw.ElapsedMilliseconds > timeout)
                    throw new TimeoutException();
            }

            lock (buffer)
            {
                return buffer.ToArray();
            }
        }
    }

    public static class ArraySegmentExtensions
    {
        public static string ToBinaryString(this ArraySegment<byte> bytes)
        {
            var sb = new StringBuilder();

            foreach (var b in bytes)
            {
                sb.AppendFormat("{0:X2} ", b);
            }

            if (sb.Length > 0)
                sb.Length--;

            return sb.ToString();
        }
    }
}
