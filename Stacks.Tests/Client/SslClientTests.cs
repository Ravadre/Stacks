using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Stacks.Tests.Client
{
    public class SslClientTests
    {
        private SocketServer server;
        private SslClient lClient, sClient;

        public SslClientTests()
        {
        }

        public void Cleanup()
        {
            server.StopAndAssertStopped();
            lClient.Close();
            sClient.Close();
        }

        private byte[] PrepareBuffer(int size)
        {
            Random rng = new Random();
            var buffer = new byte[size];
            rng.NextBytes(buffer);

            return buffer;
        }

        [Fact]
        public void Ssl_should_establish_connection()
        {
            SslHelpers.CreateServerAndConnectedClient(out server, out lClient, out sClient);

            Assert.True(lClient.IsConnected);
            Assert.True(sClient.IsConnected);

            Cleanup();
        }

        [Fact]
        public void Data_transfered_through_sockets_should_be_transfered_correctly()
        {
            SslHelpers.CreateServerAndConnectedClient(out server, out lClient, out sClient);

            var buffer = PrepareBuffer(20);

            var recvBuffer = lClient.ReceiveData(buffer.Length, 2000, () =>
                {
                    sClient.Send(buffer);
                });
            var recvBuffer2 = sClient.ReceiveData(buffer.Length, 2000, () =>
                {
                    lClient.Send(buffer);
                });

            Assert.Equal(buffer, recvBuffer);
            Assert.Equal(buffer, recvBuffer2);
        }
    }
}
