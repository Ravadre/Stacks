using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace Stacks.Tests
{
    public class RawBytesClientTests
    {
        [Fact]
        public void Client_should_connect_to_server_and_signal_appropriate_callbacks()
        {
            var connected1 = new ManualResetEventSlim(false);
            var connected2 = new ManualResetEventSlim(false);

            var ex = ServerHelpers.CreateExecutor();
            var server = ServerHelpers.CreateServer();

            server.Connected += client =>
                {
                    connected1.Set();
                };

            server.Started += () =>
            {
                var client = new SocketClient(ex);
                client.Connected += () =>
                    {
                        connected2.Set();
                    };
                client.Disconnected += exc =>
                    {
                        throw exc;
                    };
                client.Connect(new IPEndPoint(IPAddress.Loopback, server.BindEndPoint.Port));
            };

            server.Start();

            connected1.AssertWaitFor(3000);
            connected2.AssertWaitFor(3000);

            server.StopAndAssertStopped();
        }

        [Fact]
        public void Connected_clients_should_have_proper_end_points()
        {
            SocketServer server;
            SocketClient c1, c2;

            ServerHelpers.CreateServerAndConnectedClient(out server, out c1, out c2);

            Assert.Equal(c1.LocalEndPoint, c2.RemoteEndPoint);
            Assert.Equal(c1.RemoteEndPoint, c2.LocalEndPoint);

            server.StopAndAssertStopped();
        }
    }
}
