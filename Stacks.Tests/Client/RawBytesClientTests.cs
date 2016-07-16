using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Tcp;
using System.Reactive.Linq;
using Xunit;

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

            server.Connected.Subscribe(client =>
                {
                    connected1.Set();
                });

            server.Started.Subscribe(u => 
            {
                var client = new SocketClient(ex);
                client.Connected.Subscribe( _ =>
                    {
                        connected2.Set();
                    });
                client.Disconnected.Subscribe(exn =>
                    {
                        throw exn;
                    });
                client.Connect(new IPEndPoint(IPAddress.Loopback, server.BindEndPoint.Port));
            });

            server.Start();

            connected1.AssertWaitFor(5000);
            connected2.AssertWaitFor(5000);

            server.StopAndAssertStopped();
        }

        [Fact]
        public void Client_with_ipv6_should_connect_to_server_and_signal_appropriate_callbacks()
        {
            var connected1 = new ManualResetEventSlim(false);
            var connected2 = new ManualResetEventSlim(false);

            var ex = ServerHelpers.CreateExecutor();
            var server = ServerHelpers.CreateServerIPv6();

            server.Connected.Subscribe(client =>
            {
                connected1.Set();
            });

            server.Started.Subscribe(u =>
            {
                var client = new SocketClient(ex, true);
                client.Connected.Subscribe(_ =>
                {
                    connected2.Set();
                });
                client.Disconnected.Subscribe(exn =>
                {
                    throw exn;
                });
                client.Connect(new IPEndPoint(IPAddress.IPv6Loopback, server.BindEndPoint.Port));
            });

            server.Start();

            connected1.AssertWaitFor(5000);
            connected2.AssertWaitFor(5000);

            server.StopAndAssertStopped();
        }

        [Fact]
        public void Should_connect_from_dns()
        {
            var hasConnected = new ManualResetEventSlim();
            var client = new SocketClient();
            client.Connected.Subscribe(_ =>
            {
                hasConnected.Set();
            });

            client.Connect("tcp://google.com:80");

            Assert.True(hasConnected.Wait(1000));
        }

        [Fact]
        public void Calling_connect_twice_should_throw_an_exception()
        {
            var hasConnected = new ManualResetEventSlim();
            var executor = ServerHelpers.CreateExecutor();
            var server = ServerHelpers.CreateServer(executor);

            server.Started.Subscribe(_ =>
            {
                Assert.Throws(typeof(InvalidOperationException),
                    () =>
                    {
                        var client = new SocketClient(executor);
                        client.Connect(server.BindEndPoint);
                        hasConnected.Set();
                        client.Connect(server.BindEndPoint);
                    });
            });

            server.Start();

            hasConnected.AssertWaitFor();
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

        [Fact]
        public void Send_packets_should_be_received()
        {
            SocketServer server;
            SocketClient c1, c2;
            var buffer = DataHelpers.CreateRandomBuffer(204800);

            ServerHelpers.CreateServerAndConnectedClient(out server, out c1, out c2);

            var recvBuffer = c2.ReceiveData(204800, 2000, () => c1.Send(buffer));
            var recvBuffer2 = c1.ReceiveData(204800, 2000, () => c2.Send(buffer));

            Assert.Equal(buffer, recvBuffer);
            Assert.Equal(buffer, recvBuffer2);
        }

        [Fact]
        public void If_client_cannot_connect_if_should_raise_disconnect_event_with_connection_refused_error()
        {
            var disconnectedCalled = new ManualResetEventSlim();

            var executor = ServerHelpers.CreateExecutor();
            var client = new SocketClient(executor);

            client.Disconnected.Subscribe(exn =>
                {
                    Assert.IsType(typeof(SocketException), exn);
                    Assert.Equal((int)SocketError.ConnectionRefused, ((SocketException)exn).ErrorCode);
                    disconnectedCalled.Set();
                });

            client.Connect(new IPEndPoint(IPAddress.Loopback, 45232));
            disconnectedCalled.AssertWaitFor();
        }

        [Fact]
        public void If_server_closes_then_client_should_raise_disconnect_event()
        {
            var disconnectedCalled = new ManualResetEventSlim();

            SocketServer server;
            SocketClient serverClient, remoteClient;

            ServerHelpers.CreateServerAndConnectedClient(out server, out serverClient, out remoteClient);

            remoteClient.Disconnected.Subscribe(exn =>
            {
                Assert.IsType(typeof(SocketException), exn);
                Assert.Equal((int)SocketError.Disconnecting, ((SocketException)exn).ErrorCode);
                disconnectedCalled.Set();
            });
            
            serverClient.Disconnected.Subscribe(exn =>
                {
                  
                });
            server.Stop();
            serverClient.Close();
            
            disconnectedCalled.AssertWaitFor(5000);
        }
    }
}
