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
    public class SocketServerTests
    {
        protected SocketServer CreateServer()
        {
            return new SocketServer(new IPEndPoint(IPAddress.Any, 0));
        }

        public class Starting_and_stopping : SocketServerTests
        {
            [Fact]
            public void Starting_two_times_should_throw()
            {
                var server = CreateServer();
                server.Start();

                Assert.Throws(typeof(InvalidOperationException), () =>
                    {
                        server.Start();
                    });
            }

            [Fact]
            public void Starting_should_call_started_callback()
            {
                var started = new ManualResetEventSlim();
                var server = CreateServer();

                server.Started += () => { started.Set(); };
                server.Start();

                started.AssertWaitFor(2000);
            }

            [Fact]
            public void Starting_and_stopping_should_call_both_callbacks()
            {
                var started = new ManualResetEventSlim();
                var stopped = new ManualResetEventSlim();
                var server = CreateServer();

                server.Started += () => { started.Set(); };
                server.Stopped += () => { stopped.Set(); };

                server.Start();
                server.Stop();

                started.AssertWaitFor(2000);
                stopped.AssertWaitFor(2000);
            }
        }
    }
}
